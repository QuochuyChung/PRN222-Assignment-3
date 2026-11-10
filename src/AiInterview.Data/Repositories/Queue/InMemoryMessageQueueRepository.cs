using System.Collections.Concurrent;
using AiInterview.Common.Constants;
using AiInterview.Data.Models;

namespace AiInterview.Data.Repositories.Queue;

/// <summary>
/// Fake queue (singleton). ponytail: one queue per stream and the group name is ignored, because every stream
/// has exactly one consumer group in this app. Un-ACKed messages are not redelivered: there is no crash recovery in memory anyway.
/// </summary>
public sealed class InMemoryMessageQueueRepository : IMessageQueueRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<QueueMessage>> _streams = new();
    private long _nextId;

    public Task EnsureGroupAsync(string stream, string group, CancellationToken ct = default)
    {
        Queue(stream);
        return Task.CompletedTask;
    }

    public Task<string> PublishAsync(string stream, IReadOnlyDictionary<string, string> fields, CancellationToken ct = default)
    {
        var id = $"{Interlocked.Increment(ref _nextId)}-0";
        Queue(stream).Enqueue(new QueueMessage(id, new Dictionary<string, string>(fields)));
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<QueueMessage>> ReadAsync(string stream, string group, string consumer, int count, CancellationToken ct = default)
    {
        var result = new List<QueueMessage>();
        while (result.Count < count && Queue(stream).TryDequeue(out var message)) result.Add(message);
        return Task.FromResult<IReadOnlyList<QueueMessage>>(result);
    }

    public Task AckAsync(string stream, string group, string messageId, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeadLetterAsync(string stream, string group, QueueMessage message, string reason, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>(message.Fields) { ["originalId"] = message.Id, ["reason"] = reason };
        return PublishAsync(QueueNames.DeadLetter(stream), fields, ct);
    }

    /// <summary>Messages still waiting in a stream (for tests and diagnostics).</summary>
    public IReadOnlyList<QueueMessage> Peek(string stream) => Queue(stream).ToArray();

    private ConcurrentQueue<QueueMessage> Queue(string stream) => _streams.GetOrAdd(stream, _ => new());
}
