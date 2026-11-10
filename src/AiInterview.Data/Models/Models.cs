using AiInterview.Data.Entities;
using AiInterview.Common.Enums;

namespace AiInterview.Data.Models;

/// <summary>Metadata tài liệu (lưu ở MongoDB). Xem tasks.md, Phụ lục B.</summary>
public class DocumentRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "";

    public long Size { get; set; }

    /// <summary>Key của file trong MinIO: <c>{documentId}/{fileName}</c>.</summary>
    public string StorageKey { get; set; } = "";

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    public string? ErrorMessage { get; set; }

    public int ChunkCount { get; set; }

    public int PageCount { get; set; }

    public string? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Một điểm vector trong Qdrant. <see cref="Id"/> = <c>{documentId}:{chunkIndex}</c>.</summary>
public sealed record VectorPoint(
    string Id,
    float[] Vector,
    string DocumentId,
    int ChunkIndex,
    int Page,
    string Text);

public sealed record VectorHit(
    string Id,
    float Score,
    string DocumentId,
    int ChunkIndex,
    int Page,
    string Text);

/// <summary>Giới hạn phạm vi tìm kiếm (null = tìm trên toàn bộ).</summary>
public sealed record VectorFilter(IReadOnlyList<string>? DocumentIds = null);

/// <summary>Trạng thái phiên thi đang chạy (lưu ở Redis, Hash <c>interview:{sessionId}</c>).</summary>
public class SessionState
{
    public string SessionId { get; set; } = "";

    public InterviewState State { get; set; } = InterviewState.AskQuestion;

    public int QuestionIndex { get; set; }

    public Guid? QuestionId { get; set; }

    public int FollowUpCount { get; set; }

    /// <summary>Thời điểm hết giờ (UTC, tuyệt đối). Server trả <c>remainingSeconds = Deadline - now</c> cho client.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Danh sách câu hỏi chính của phiên, theo thứ tự hỏi.</summary>
    public List<Guid> QuestionIds { get; set; } = new();

    /// <summary>
    /// Số lần đã nhắc vì im lặng trong câu hỏi hiện tại. Luật cứng ENG-05: nhắc một lần rồi bỏ qua câu.
    /// Đếm lại từ 0 mỗi khi sang câu mới. Chỉ lưu trong Redis/in-memory, không thêm cột SQL.
    /// </summary>
    public int SilencePrompts { get; set; }
}

/// <summary>Một message trong Redis Stream.</summary>
public sealed record QueueMessage(string Id, IReadOnlyDictionary<string, string> Fields);
