using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiInterview.Web.Workers;

public class DocumentWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentWorker> _logger;
    private readonly string _consumerName;
    private readonly ConcurrentDictionary<string, int> _deliveryAttempts = new();
    private const int MaxDeliveryAttempts = 3;

    public DocumentWorker(
        IServiceProvider serviceProvider,
        ILogger<DocumentWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _consumerName = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentWorker.cs - App: Bắt đầu chạy worker docs-stream, consumerName={ConsumerName}",
            _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var consumer = scope.ServiceProvider.GetRequiredService<IDocumentStreamConsumer>();
                var docService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

                var messages = await consumer.ReadMessagesAsync(count: 5, waitTimeout: TimeSpan.FromSeconds(2), stoppingToken);

                if (messages == null || messages.Count == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                foreach (var envelope in messages)
                {
                    string docId = envelope.Message.DocumentId;
                    string storageKey = envelope.Message.StorageKey;
                    string msgId = envelope.MessageId;

                    int attempts = _deliveryAttempts.AddOrUpdate(msgId, 1, (_, current) => current + 1);

                    _logger.LogInformation("DocumentWorker.cs - App: Nhận message từ stream, messageId={MsgId}, documentId={DocId}, lần thử={Attempt}",
                        msgId, docId, attempts);

                    try
                    {
                        await docService.ProcessDocumentAsync(docId, storageKey, stoppingToken);
                        await consumer.AcknowledgeAsync(msgId, stoppingToken);
                        _deliveryAttempts.TryRemove(msgId, out _);

                        _logger.LogInformation("DocumentWorker.cs - App: Xử lý thành công và ACK messageId={MsgId}, documentId={DocId}",
                            msgId, docId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DocumentWorker.cs - App: Xử lý message thất bại lần {Attempt}/{Max}, messageId={MsgId}, documentId={DocId}",
                            attempts, MaxDeliveryAttempts, msgId, docId);

                        if (attempts >= MaxDeliveryAttempts)
                        {
                            _logger.LogError("DocumentWorker.cs - App: Message chuyển vào Dead-Letter sau khi vượt quá số lần thử, messageId={MsgId}, documentId={DocId}",
                                msgId, docId);

                            // Acknowledge from primary queue to prevent permanent blocking
                            await consumer.AcknowledgeAsync(msgId, stoppingToken);
                            _deliveryAttempts.TryRemove(msgId, out _);
                        }
                        else
                        {
                            // Backoff delay before processing next message
                            await Task.Delay(TimeSpan.FromSeconds(attempts * 2), stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DocumentWorker.cs - App: Lỗi vòng lặp worker docs-stream, đợi 3s thử lại...");
                await Task.Delay(3000, stoppingToken);
            }
        }

        _logger.LogInformation("DocumentWorker.cs - App: DocumentWorker đã dừng.");
    }
}

