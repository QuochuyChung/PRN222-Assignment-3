using AiInterview.Common.Logging;
using AiInterview.Common.Enums;
using AiInterview.DAL.Repositories.Documents;
using AiInterview.DAL.Repositories.Questions;
using AiInterview.DAL.Repositories.Sessions;
using AiInterview.DAL.Repositories.Turns;
using Microsoft.Extensions.Logging;

namespace AiInterview.BLL.Dashboard;

public sealed record RecentTurnItem(
    string TurnId,
    string QuestionText,
    string TranscriptPreview,
    bool IsFollowUp,
    DateTime EndedAt);

public sealed record DashboardSummary(
    int TotalDocuments,
    int ProcessedDocuments,
    int DraftQuestions,
    int ApprovedQuestions,
    int TotalSessions,
    int CompletedSessions,
    int TotalTurns,
    IReadOnlyList<RecentTurnItem> RecentTurns);

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);
}

/// <summary>
/// Ví dụ chuẩn của kiến trúc 3 lớp: PageModel -> Service -> Repository.
/// Service chỉ gọi interface repository, không biết đang chạy Fake hay Real.
/// </summary>
internal sealed class DashboardService(
    IDocumentRepository documents,
    IQuestionRepository questions,
    ISessionRepository sessions,
    ITurnRepository turns,
    ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalDocuments = await documents.CountAsync(null, ct);
        var processedDocuments = await documents.CountAsync(DocumentStatus.Success, ct);
        var draftQuestions = await questions.CountAsync(QuestionStatus.Draft, ct);
        var approvedQuestions = await questions.CountAsync(QuestionStatus.Approved, ct);
        var totalSessions = await sessions.CountAsync(null, ct);
        var completedSessions = await sessions.CountAsync(SessionStatus.Completed, ct);
        var totalTurns = await turns.CountAsync(ct);

        var recent = (await turns.ListRecentAsync(8, ct))
            .Select(t => new RecentTurnItem(
                t.TurnId,
                t.QuestionText,
                LogText.Preview(t.Transcript, 70),
                t.IsFollowUp,
                t.EndedAt))
            .ToList();

        logger.Info("DashboardService.cs", ServiceNames.App,
            "Tổng hợp dashboard: documents={Documents}, questions={Questions}, sessions={Sessions}, turns={Turns}",
            totalDocuments, draftQuestions + approvedQuestions, totalSessions, totalTurns);

        return new DashboardSummary(
            totalDocuments, processedDocuments,
            draftQuestions, approvedQuestions,
            totalSessions, completedSessions,
            totalTurns, recent);
    }
}
