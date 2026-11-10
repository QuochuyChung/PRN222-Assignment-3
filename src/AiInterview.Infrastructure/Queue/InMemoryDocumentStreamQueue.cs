using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Queue;

public class InMemoryDocumentStreamQueue : IDocumentStreamPublisher, IDocumentStreamConsumer
{
    private readonly Channel<DocStreamMessageEnvelope> _channel;
    private readonly ILogger<InMemoryDocumentStreamQueue> _logger;
    private int _idCounter;

    public InMemoryDocumentStreamQueue(ILogger<InMemoryDocumentStreamQueue> logger)
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<DocStreamMessageEnvelope>(options);
        _logger = logger;
    }

    public async Task PublishAsync(DocStreamMessage message, CancellationToken ct = default)
    {
        string messageId = Interlocked.Increment(ref _idCounter).ToString();
        var envelope = new DocStreamMessageEnvelope
        {
            MessageId = messageId,
            Message = message,
            DeliveryCount = 1
        };

        await _channel.Writer.WriteAsync(envelope, ct);
        _logger.LogInformation("InMemoryDocumentStreamQueue.cs - App: Ghi vào stream giả lập thành công, messageId={MsgId}, documentId={DocId}",
            messageId, message.DocumentId);
    }

    public async Task<IReadOnlyList<DocStreamMessageEnvelope>> ReadMessagesAsync(int count, TimeSpan waitTimeout, CancellationToken ct = default)
    {
        var list = new List<DocStreamMessageEnvelope>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(waitTimeout);

        try
        {
            while (list.Count < count)
            {
                if (await _channel.Reader.WaitToReadAsync(cts.Token))
                {
                    if (_channel.Reader.TryRead(out var envelope))
                    {
                        list.Add(envelope);
                    }
                }
                else
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached, return whatever was collected
        }

        return list;
    }

    public Task AcknowledgeAsync(string messageId, CancellationToken ct = default)
    {
        _logger.LogInformation("InMemoryDocumentStreamQueue.cs - App: ACK messageId={MsgId}", messageId);
        return Task.CompletedTask;
    }
}

