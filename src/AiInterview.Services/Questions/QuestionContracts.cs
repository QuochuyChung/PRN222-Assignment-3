using AiInterview.Common.Enums;

namespace AiInterview.Services.Questions;

/// <summary>Câu hỏi trả cho giao diện (không phải entity của lớp Data).</summary>
public sealed record QuestionDto(
    Guid Id,
    string Content,
    string ReferenceAnswer,
    IReadOnlyList<string> KeyPoints,
    BloomLevel BloomLevel,
    string? SourceDocumentId,
    IReadOnlyList<string> SourceChunkIds,
    QuestionStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Nội dung giảng viên sửa khi duyệt câu hỏi.</summary>
public sealed record UpdateQuestionRequest(
    Guid Id,
    string Content,
    string ReferenceAnswer,
    IReadOnlyList<string> KeyPoints,
    BloomLevel BloomLevel);

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
    Task<IReadOnlyList<QuestionDto>> ListAsync(QuestionStatus? status = null, CancellationToken ct = default);

    Task<QuestionDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lưu các câu hỏi AI sinh ra với trạng thái <see cref="QuestionStatus.Draft"/>.</summary>
    Task<IReadOnlyList<QuestionDto>> SaveDraftsAsync(
        IEnumerable<QuestionDraft> drafts,
        string? sourceDocumentId,
        CancellationToken ct = default);

    Task UpdateAsync(UpdateQuestionRequest request, CancellationToken ct = default);

    /// <summary>Chỉ câu <see cref="QuestionStatus.Approved"/> mới vào ngân hàng chính thức.</summary>
    Task ApproveAsync(Guid id, CancellationToken ct = default);

    Task RejectAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
