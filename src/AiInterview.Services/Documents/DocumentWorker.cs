using AiInterview.Common.Constants;
using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.Data.Models;
using AiInterview.Data.Repositories.Documents;
using AiInterview.Data.Repositories.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Services.Documents;

/// <summary>
/// ING-07: consumes <c>docs-stream</c> in group <c>docs-workers</c>; the consumer name is the pod name (HOSTNAME).
/// Each message gets <see cref="IngestionOptions.MaxAttempts"/> tries with exponential backoff; after that the document
/// is marked Failed and the message moves to <c>docs-stream-dead</c>. On shutdown the message is left un-ACKed
/// so it is reclaimed later.
/// </summary>
internal sealed class DocumentWorker(
    IServiceScopeFactory scopes,
    IMessageQueueRepository queue,
    IOptions<IngestionOptions> options,
    ILogger<DocumentWorker> logger) : BackgroundService
{
    private const string File = "DocumentWorker.cs";
    private readonly string _consumer = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        logger.Info(File, ServiceNames.App, "Started, stream={Stream}, group={Group}, consumer={Consumer}",
            QueueNames.DocsStream, QueueNames.DocsGroup, _consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await queue.EnsureGroupAsync(QueueNames.DocsStream, QueueNames.DocsGroup, stoppingToken);
                var messages = await queue.ReadAsync(QueueNames.DocsStream, QueueNames.DocsGroup, _consumer, 5, stoppingToken);
                foreach (var message in messages)
                    await HandleAsync(message, o, stoppingToken);

                if (messages.Count == 0) await Task.Delay(o.PollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.Error(ex, File, ServiceNames.App, "Worker loop error, retrying in 5s, consumer={Consumer}", _consumer);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }

        logger.Info(File, ServiceNames.App, "Stopped, consumer={Consumer}", _consumer);
    }

    private async Task HandleAsync(QueueMessage message, IngestionOptions o, CancellationToken ct)
    {
        if (!message.Fields.TryGetValue("documentId", out var documentId) || string.IsNullOrWhiteSpace(documentId))
        {
            await queue.DeadLetterAsync(QueueNames.DocsStream, QueueNames.DocsGroup, message, "Message has no documentId.", ct);
            return;
        }

        var maxAttempts = Math.Max(1, o.MaxAttempts);
        string? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>().ProcessAsync(documentId, ct);
                await queue.AckAsync(QueueNames.DocsStream, QueueNames.DocsGroup, message.Id, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                logger.Warn(File, ServiceNames.App, "Attempt {Attempt}/{Max} failed, documentId={DocId}, messageId={MessageId}: {Error}",
                    attempt, maxAttempts, documentId, message.Id, ex.Message);
                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(o.RetryBaseDelayMs * Math.Pow(2, attempt - 1)), ct);
            }
        }

        var reason = $"Processing failed after {maxAttempts} attempts: {lastError}";
        logger.Error(File, ServiceNames.App, "Giving up, moving to dead-letter, documentId={DocId}, messageId={MessageId}", documentId, message.Id);

        using (var scope = scopes.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentRepository>()
                .UpdateStatusAsync(documentId, DocumentStatus.Failed, reason, 0, 0, ct);
            await scope.ServiceProvider.GetRequiredService<IDocumentProgressNotifier>()
                .NotifyAsync(new DocumentProgress(documentId, DocumentStatus.Failed, 100, "Failed", reason), ct);
        }

        await queue.DeadLetterAsync(QueueNames.DocsStream, QueueNames.DocsGroup, message, reason, ct);
    }
}

/// <summary>Default notifier when no UI is attached (worker-only process, tests).</summary>
internal sealed class NullDocumentProgressNotifier : IDocumentProgressNotifier
{
    public Task NotifyAsync(DocumentProgress progress, CancellationToken ct = default) => Task.CompletedTask;

    public Task NotifyDeletedAsync(string documentId, CancellationToken ct = default) => Task.CompletedTask;
}
