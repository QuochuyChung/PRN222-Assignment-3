using AiInterview.Data.Entities;
using AiInterview.Services.Interview;

namespace AiInterview.Services.TurnLog;

/// <summary>Ghi và xem log từng lượt thi. Dev B (LOG-01, LOG-02).</summary>
public interface ITurnLogService
{
    /// <summary>Ghi idempotent theo TurnId. Trả true nếu ghi mới, false nếu đã có (worker coi là thành công).</summary>
    Task<bool> SaveAsync(TurnMessage turn, CancellationToken ct = default);

    Task<IReadOnlyList<InterviewTurn>> ListBySessionAsync(Guid sessionId, CancellationToken ct = default);

    Task<IReadOnlyList<InterviewTurn>> ListRecentAsync(int take, CancellationToken ct = default);
}
