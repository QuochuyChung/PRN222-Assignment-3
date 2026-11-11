namespace AiInterview.DAL.Repositories.Sessions;

/// <summary>
/// ENG-02: ghi nhớ câu hỏi vừa được dùng cho các phiên gần đây để thí sinh thi liền nhau không nhận
/// cùng một tập câu. Real = Redis (list có TTL), Fake = in-memory.
/// Không dùng <c>InterviewTurn</c> vì bảng đó do LOG-01 ghi, có thể chậm hoặc chưa chạy.
/// </summary>
public interface IQuestionUsageRepository
{
    /// <summary>Ghi các câu hỏi vừa dùng cho một phiên.</summary>
    Task RecordAsync(Guid sessionId, IReadOnlyList<Guid> questionIds, CancellationToken ct = default);

    /// <summary>Câu hỏi đã dùng gần đây, mới nhất trước, tối đa <paramref name="take"/> id.</summary>
    Task<IReadOnlyList<Guid>> ListRecentAsync(int take, CancellationToken ct = default);
}
