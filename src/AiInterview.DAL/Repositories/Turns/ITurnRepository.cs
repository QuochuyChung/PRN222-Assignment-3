using AiInterview.DAL.Entities;
using AiInterview.Common.Enums;

namespace AiInterview.DAL.Repositories.Turns;

/// <summary>Log từng lượt hỏi/đáp (SQL). Dev B (LOG-01) dùng để ghi từ <c>turns-stream</c>.</summary>
public interface ITurnRepository
{
    /// <summary>Ghi idempotent theo <c>TurnId</c>. Trả true nếu vừa ghi mới, false nếu đã tồn tại.</summary>
    Task<bool> InsertIfNotExistsAsync(InterviewTurn turn, CancellationToken ct = default);

    Task<IReadOnlyList<InterviewTurn>> ListBySessionAsync(Guid sessionId, CancellationToken ct = default);

    Task<IReadOnlyList<InterviewTurn>> ListRecentAsync(int take, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);
}
