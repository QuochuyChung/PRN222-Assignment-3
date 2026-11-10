using AiInterview.Services.Interview;

namespace AiInterview.Services.TurnLog;

/// <summary>Một lượt hỏi/đáp trả cho giao diện (không phải entity của lớp Data).</summary>
public sealed record TurnDto(
    string TurnId,
    Guid SessionId,
    Guid? QuestionId,
    bool IsFollowUp,
    string QuestionText,
    string Transcript,
    bool NeedFollowUp,
    string? Reason,
    IReadOnlyList<string> Sources,
    DateTime StartedAt,
    DateTime EndedAt);

/// <summary>Ghi và xem log lượt thi. Dev B (LOG-01, LOG-02).</summary>
public interface ITurnLogService
{
    /// <summary>Ghi idempotent theo TurnId. Trả true nếu ghi mới, false nếu đã có (worker coi là thành công).</summary>
    Task<bool> SaveAsync(TurnMessage turn, CancellationToken ct = default);

    Task<IReadOnlyList<TurnDto>> ListBySessionAsync(Guid sessionId, CancellationToken ct = default);

    Task<IReadOnlyList<TurnDto>> ListRecentAsync(int take, CancellationToken ct = default);
}
