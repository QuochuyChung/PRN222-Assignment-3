using AiInterview.Data.Models;

namespace AiInterview.Data.Repositories.Queue;

/// <summary>Hàng đợi message. Real = Redis Streams (consumer group, ACK, dead-letter). Fake = in-memory.</summary>
public interface IMessageQueueRepository
{
    Task EnsureGroupAsync(string stream, string group, CancellationToken ct = default);

    /// <summary>Đẩy message vào stream, trả về id message.</summary>
    Task<string> PublishAsync(string stream, IReadOnlyDictionary<string, string> fields, CancellationToken ct = default);

    Task<IReadOnlyList<QueueMessage>> ReadAsync(
        string stream,
        string group,
        string consumer,
        int count,
        CancellationToken ct = default);

    Task AckAsync(string stream, string group, string messageId, CancellationToken ct = default);

    /// <summary>Chuyển message lỗi quá số lần thử sang stream <c>{stream}-dead</c> và ACK bản gốc.</summary>
    Task DeadLetterAsync(string stream, string group, QueueMessage message, string reason, CancellationToken ct = default);
}
