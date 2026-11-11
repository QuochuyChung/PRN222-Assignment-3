using AiInterview.Common.Enums;
using AiInterview.BLL.Retrieval;

namespace AiInterview.BLL.Interview;

public sealed record SessionDto(
    Guid Id,
    string Title,
    string StudentName,
    SessionStatus Status,
    int QuestionCount,
    int MaxFollowUps,
    int AnswerSeconds,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record CreateSessionRequest(
    string Title,
    string StudentName,
    int? QuestionCount = null,
    int? MaxFollowUps = null,
    int? AnswerSeconds = null);

/// <summary>Trạng thái phiên thi trả cho client. <see cref="RemainingSeconds"/> do server tính từ deadline.</summary>
public sealed record InterviewStateView(
    Guid SessionId,
    InterviewState State,
    int QuestionIndex,
    int QuestionCount,
    string? CurrentQuestionText,
    bool IsFollowUp,
    int FollowUpCount,
    int MaxFollowUps,
    int RemainingSeconds);

/// <summary><see cref="TurnId"/> do client sinh; gửi lặp cùng TurnId không được xử lý hai lần.</summary>
public sealed record SubmitAnswerRequest(Guid SessionId, string TurnId, string Transcript);

public sealed record SubmitAnswerResult(InterviewStateView State, FollowUpDecision? Decision);

public sealed record FollowUpRequest(
    string QuestionText,
    string ReferenceAnswer,
    IReadOnlyList<string> KeyPoints,
    string Transcript,
    int FollowUpCount,
    int MaxFollowUps,
    IReadOnlyList<RetrievedChunk> ContextChunks);

/// <summary>Kết quả LLM. JSON mẫu xem tasks.md, Phụ lục C.</summary>
public sealed record FollowUpDecision(
    bool NeedFollowUp,
    string? FollowUpQuestion,
    string Reason,
    IReadOnlyList<string> CoveredKeyPoints,
    IReadOnlyList<string> MissingKeyPoints,
    IReadOnlyList<string> Sources);

/// <summary>Vòng đời phiên thi và state machine. Dev C (ENG-02, ENG-03, ENG-05..07, ENG-09).</summary>
public interface IInterviewSessionService
{
    Task<IReadOnlyList<SessionDto>> ListAsync(SessionStatus? status = null, CancellationToken ct = default);

    Task<SessionDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<SessionDto> CreateAsync(CreateSessionRequest request, CancellationToken ct = default);

    Task<InterviewStateView> StartAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Dùng khi tải lại trang (F5): trả đúng câu hiện tại, số lượt xoáy đã dùng và thời gian còn lại.</summary>
    Task<InterviewStateView?> GetStateAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Luật cứng (hết giờ, hết lượt xoáy) do server quyết định, không tin hoàn toàn vào LLM.</summary>
    Task<SubmitAnswerResult> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct = default);

    Task<InterviewStateView> SkipAsync(Guid sessionId, CancellationToken ct = default);

    Task FinishAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>Quyết định có hỏi xoáy không: dựng prompt, gọi ILlmRepository, parse JSON. Dev C (ENG-04).</summary>
public interface IFollowUpDecider
{
    Task<FollowUpDecision> DecideAsync(FollowUpRequest request, CancellationToken ct = default);
}
