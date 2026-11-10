using AiInterview.Data.Entities;

namespace AiInterview.Services.Questions;

public sealed record GenerateQuestionsRequest(
    IReadOnlyList<string> DocumentIds,
    int Count,
    IReadOnlyList<BloomLevel>? Levels = null);

/// <summary>Câu hỏi do LLM sinh, chưa lưu. JSON mẫu xem tasks.md, Phụ lục C.</summary>
public sealed record QuestionDraft(
    string Content,
    string ReferenceAnswer,
    IReadOnlyList<string> KeyPoints,
    BloomLevel BloomLevel,
    IReadOnlyList<string> SourceChunkIds);

/// <summary>Sinh câu hỏi từ tài liệu: truy xuất chunk, dựng prompt, gọi ILlmRepository, parse JSON. Dev B (QST-02).</summary>
public interface IQuestionGeneratorService
{
    Task<IReadOnlyList<QuestionDraft>> GenerateAsync(GenerateQuestionsRequest request, CancellationToken ct = default);
}

/// <summary>Ngân hàng câu hỏi: lưu bản nháp, duyệt/sửa/loại bỏ. Dev B (QST-03..05).</summary>
public interface IQuestionService
{
    Task<IReadOnlyList<Question>> ListAsync(QuestionStatus? status = null, CancellationToken ct = default);

    Task<Question?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lưu các câu hỏi AI sinh ra với trạng thái <see cref="QuestionStatus.Draft"/>.</summary>
    Task<IReadOnlyList<Question>> SaveDraftsAsync(
        IEnumerable<QuestionDraft> drafts,
        string? sourceDocumentId,
        CancellationToken ct = default);

    Task UpdateAsync(Question question, CancellationToken ct = default);

    /// <summary>Chỉ câu <see cref="QuestionStatus.Approved"/> mới vào ngân hàng chính thức.</summary>
    Task ApproveAsync(Guid id, CancellationToken ct = default);

    Task RejectAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
