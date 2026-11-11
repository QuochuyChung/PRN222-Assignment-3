namespace AiInterview.BLL.Retrieval;

public sealed record RetrievalScope(IReadOnlyList<string>? DocumentIds = null);

public sealed record RetrievedChunk(string Id, string DocumentId, int Page, string Text, float Score);

/// <summary>Truy xuất đoạn tài liệu liên quan (embed câu truy vấn rồi tìm vector). Dev B (QST-01).</summary>
public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string query,
        RetrievalScope scope,
        int topK,
        CancellationToken ct = default);
}
