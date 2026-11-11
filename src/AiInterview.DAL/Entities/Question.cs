using AiInterview.Common.Enums;

namespace AiInterview.DAL.Entities;

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Content { get; set; } = "";

    public string ReferenceAnswer { get; set; } = "";

    public List<string> KeyPoints { get; set; } = new();

    public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;

    /// <summary>Id tài liệu (Mongo) làm nguồn sinh câu hỏi. Không có khóa ngoại vì nằm ở DB khác.</summary>
    public string? SourceDocumentId { get; set; }

    /// <summary>Id các chunk (Qdrant) đã dùng, dạng <c>{documentId}:{chunkIndex}</c>.</summary>
    public List<string> SourceChunkIds { get; set; } = new();

    public QuestionStatus Status { get; set; } = QuestionStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
