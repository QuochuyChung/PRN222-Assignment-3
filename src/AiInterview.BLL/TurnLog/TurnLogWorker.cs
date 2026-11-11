using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using AiInterview.DAL.Models;
using AiInterview.DAL.Repositories.Queue;
using AiInterview.BLL.Interview;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiInterview.BLL.TurnLog;

internal sealed class TurnLogWorker(IServiceScopeFactory scopes, IMessageQueueRepository queue, ILogger<TurnLogWorker> logger) : BackgroundService
{
    private const string File = "TurnLogWorker.cs";
    private readonly string _consumer = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await queue.EnsureGroupAsync(QueueNames.TurnsStream, QueueNames.TurnsGroup, stoppingToken);
                var messages = await queue.ReadAsync(QueueNames.TurnsStream, QueueNames.TurnsGroup, _consumer, 5, stoppingToken);
                foreach (var message in messages) await HandleAsync(message, stoppingToken);
                if (messages.Count == 0) await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.Error(ex, File, ServiceNames.App, "Worker loop failed, stream={Stream}, consumer={Consumer}", QueueNames.TurnsStream, _consumer);
                try { await Task.Delay(5000, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task HandleAsync(QueueMessage message, CancellationToken ct)
    {
        var turnId = message.Fields.GetValueOrDefault("turnId");
        Exception? last = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var turn = TurnMessage.FromFields(message.Fields);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ITurnLogService>().SaveAsync(turn, ct);
                await queue.AckAsync(QueueNames.TurnsStream, QueueNames.TurnsGroup, message.Id, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                last = ex;
                logger.Error(ex, File, ServiceNames.App, "Turn write failed, turnId={TurnId}, messageId={MessageId}, attempt={Attempt}", turnId, message.Id, attempt);
                if (attempt < 3) await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)), ct);
            }
        }
        await queue.DeadLetterAsync(QueueNames.TurnsStream, QueueNames.TurnsGroup, message, last?.Message ?? "Unknown error", ct);
    }
}
