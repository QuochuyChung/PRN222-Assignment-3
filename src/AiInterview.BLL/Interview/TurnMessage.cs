using System.Globalization;
using System.Text.Json;
using AiInterview.DAL.Entities;
using AiInterview.Common.Enums;

namespace AiInterview.BLL.Interview;

/// <summary>
/// Một lượt hỏi/đáp được đẩy vào <c>turns-stream</c>. Dev C (publisher, ENG-08) và Dev B (consumer, LOG-01)
/// dùng chung class này để chuyển qua lại với message Redis và entity SQL, tránh lệch định dạng.
/// </summary>
public sealed record TurnMessage(
    string TurnId,
    Guid SessionId,
    Guid? QuestionId,
    string Question,
    string Transcript,
    bool IsFollowUp,
    bool NeedFollowUp,
    string? Reason,
    IReadOnlyList<string> Sources,
    DateTime StartedAt,
    DateTime EndedAt)
{
    public IReadOnlyDictionary<string, string> ToFields() => new Dictionary<string, string>
    {
        ["turnId"] = TurnId,
        ["sessionId"] = SessionId.ToString(),
        ["questionId"] = QuestionId?.ToString() ?? "",
        ["question"] = Question,
        ["transcript"] = Transcript,
        ["isFollowUp"] = IsFollowUp.ToString(),
        ["needFollowUp"] = NeedFollowUp.ToString(),
        ["reason"] = Reason ?? "",
        ["sources"] = JsonSerializer.Serialize(Sources),
        ["startedAt"] = StartedAt.ToString("O", CultureInfo.InvariantCulture),
        ["endedAt"] = EndedAt.ToString("O", CultureInfo.InvariantCulture)
    };

    public static TurnMessage FromFields(IReadOnlyDictionary<string, string> fields)
    {
        string Get(string key) => fields.TryGetValue(key, out var value) ? value : "";

        var questionId = Get("questionId");
        var sources = Get("sources");

        return new TurnMessage(
            TurnId: Get("turnId"),
            SessionId: Guid.Parse(Get("sessionId")),
            QuestionId: Guid.TryParse(questionId, out var q) ? q : null,
            Question: Get("question"),
            Transcript: Get("transcript"),
            IsFollowUp: bool.Parse(Get("isFollowUp")),
            NeedFollowUp: bool.Parse(Get("needFollowUp")),
            Reason: Get("reason") is { Length: > 0 } reason ? reason : null,
            Sources: string.IsNullOrEmpty(sources)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<List<string>>(sources) ?? new List<string>(),
            StartedAt: DateTime.Parse(Get("startedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            EndedAt: DateTime.Parse(Get("endedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    internal InterviewTurn ToEntity() => new()
    {
        TurnId = TurnId,
        SessionId = SessionId,
        QuestionId = QuestionId,
        QuestionText = Question,
        Transcript = Transcript,
        IsFollowUp = IsFollowUp,
        NeedFollowUp = NeedFollowUp,
        Reason = Reason,
        Sources = Sources.ToList(),
        StartedAt = StartedAt,
        EndedAt = EndedAt
    };
}

/// <summary>Đẩy lượt vừa hoàn tất vào <c>turns-stream</c> qua IMessageQueueRepository. Dev C (ENG-08).</summary>
public interface ITurnPublisher
{
    Task PublishAsync(TurnMessage turn, CancellationToken ct = default);
}
