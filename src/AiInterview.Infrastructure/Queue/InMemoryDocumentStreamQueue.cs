using System;
using System.Collections.Concurrent;
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
    private readonly ConcurrentQueue<DeadLetterEntry> _deadLetters = new();
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

    /// <summary>Messages that were moved to the dead-letter list. Useful for tests and diagnostics.</summary>
    public IReadOnlyCollection<DeadLetterEntry> DeadLetters => _deadLetters.ToArray();

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
        _logger.LogInformation("InMemoryDocumentStreamQueue.cs - App: Wrote message to in-memory stream, messageId={MsgId}, documentId={DocId}",
            messageId, message.DocumentId);
    }

    public async Task<IReadOnlyList<DocStreamMessageEnvelope>> ReadMessagesAsync(int count, TimeSpan waitTimeout, CancellationToken ct = default)
    {
        var list = new List<DocStreamMessageEnvelope>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(waitTimeout);

        try
        {
            // Block until at least one message arrives (or the timeout hits),
            // then return everything that is already available, up to `count`.
            if (await _channel.Reader.WaitToReadAsync(cts.Token))
            {
                while (list.Count < count && _channel.Reader.TryRead(out var envelope))
                {
                    list.Add(envelope);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Wait timeout reached with no messages.
        }

        return list;
    }

    public Task AcknowledgeAsync(string messageId, CancellationToken ct = default)
    {
        _logger.LogInformation("InMemoryDocumentStreamQueue.cs - App: ACK messageId={MsgId}", messageId);
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(DocStreamMessageEnvelope envelope, string reason, CancellationToken ct = default)
    {
        _deadLetters.Enqueue(new DeadLetterEntry(envelope, reason, DateTime.UtcNow));
        _logger.LogWarning("InMemoryDocumentStreamQueue.cs - App: Moved message to dead-letter list, messageId={MsgId}, documentId={DocId}, reason={Reason}",
            envelope.MessageId, envelope.Message.DocumentId, reason);
        return Task.CompletedTask;
    }
}

public record DeadLetterEntry(DocStreamMessageEnvelope Envelope, string Reason, DateTime FailedAt);
