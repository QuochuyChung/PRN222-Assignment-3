using System;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Web.Workers;

public class DocumentWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentWorker> _logger;
    private readonly string _consumerName;
    private readonly int _maxDeliveryAttempts;
    private readonly TimeSpan _retryBaseDelay;

    public DocumentWorker(
        IServiceProvider serviceProvider,
        ILogger<DocumentWorker> logger,
        IOptions<AiInterviewOptions>? options = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _consumerName = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;

        var workerOptions = options?.Value.Worker ?? new WorkerOptions();
        _maxDeliveryAttempts = Math.Max(1, workerOptions.MaxDeliveryAttempts);
        _retryBaseDelay = TimeSpan.FromMilliseconds(Math.Max(0, workerOptions.RetryBaseDelayMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentWorker.cs - App: docs-stream worker started, consumerName={ConsumerName}",
            _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var consumer = scope.ServiceProvider.GetRequiredService<IDocumentStreamConsumer>();

                var messages = await consumer.ReadMessagesAsync(count: 5, waitTimeout: TimeSpan.FromSeconds(2), stoppingToken);

                if (messages == null || messages.Count == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                foreach (var envelope in messages)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await HandleMessageAsync(scope.ServiceProvider, consumer, envelope, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DocumentWorker.cs - App: docs-stream worker loop error, retrying in 3s");
                try
                {
                    await Task.Delay(3000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("DocumentWorker.cs - App: DocumentWorker stopped.");
    }

    private async Task HandleMessageAsync(
        IServiceProvider services,
        IDocumentStreamConsumer consumer,
        DocStreamMessageEnvelope envelope,
        CancellationToken stoppingToken)
    {
        var docService = services.GetRequiredService<IDocumentService>();
        var docRepo = services.GetRequiredService<IDocumentRepository>();
        var notifier = services.GetService<IDocumentProgressNotifier>();

        string docId = envelope.Message.DocumentId;
        string storageKey = envelope.Message.StorageKey;
        string msgId = envelope.MessageId;
        string? lastErrorMessage = null;

        for (int attempt = 1; attempt <= _maxDeliveryAttempts; attempt++)
        {
            _logger.LogInformation("DocumentWorker.cs - App: Processing messageId={MsgId}, documentId={DocId}, attempt={Attempt}/{Max}",
                msgId, docId, attempt, _maxDeliveryAttempts);

            try
            {
                await docService.ProcessDocumentAsync(docId, storageKey, stoppingToken);
                await consumer.AcknowledgeAsync(msgId, stoppingToken);

                _logger.LogInformation("DocumentWorker.cs - App: Processed and ACKed messageId={MsgId}, documentId={DocId}",
                    msgId, docId);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: leave the message un-ACKed so it is picked up again on the next start.
                throw;
            }
            catch (Exception ex)
            {
                lastErrorMessage = ex.Message;
                _logger.LogWarning(ex, "DocumentWorker.cs - App: Attempt {Attempt}/{Max} failed, messageId={MsgId}, documentId={DocId}",
                    attempt, _maxDeliveryAttempts, msgId, docId);

                if (attempt < _maxDeliveryAttempts)
                {
                    // Exponential backoff: base, base*2, base*4, ...
                    var delay = TimeSpan.FromMilliseconds(_retryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }

        string reason = $"Processing failed after {_maxDeliveryAttempts} attempts: {lastErrorMessage}";

        _logger.LogError("DocumentWorker.cs - App: Moving message to dead-letter after {Max} failed attempts, messageId={MsgId}, documentId={DocId}",
            _maxDeliveryAttempts, msgId, docId);

        await docRepo.UpdateStatusAsync(docId, DocumentStatus.Failed, errorMessage: reason, ct: stoppingToken);

        if (notifier != null)
        {
            await notifier.NotifyStatusChangedAsync(docId, DocumentStatus.Failed.ToString(), reason, 0, 0);
        }

        // Copies the message to the dead-letter stream, then ACKs it on the main stream.
        await consumer.DeadLetterAsync(envelope, reason, stoppingToken);
    }
}
