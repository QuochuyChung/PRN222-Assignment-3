namespace AiInterview.Data.Entities;

/// <summary>Một lượt hỏi/đáp (câu chính hoặc câu hỏi xoáy). <see cref="TurnId"/> là khóa duy nhất để ghi idempotent.</summary>
public class InterviewTurn
{
    /// <summary>Do client/engine sinh (ví dụ GUID dạng chuỗi). Là khóa chính nên không thể ghi trùng.</summary>
    public string TurnId { get; set; } = Guid.NewGuid().ToString("N");

    public Guid SessionId { get; set; }

    public InterviewSession? Session { get; set; }

    /// <summary>Câu hỏi chính mà lượt này thuộc về. Không đặt khóa ngoại để không chặn việc xóa câu hỏi.</summary>
    public Guid? QuestionId { get; set; }

    public bool IsFollowUp { get; set; }

    public string QuestionText { get; set; } = "";

    public string Transcript { get; set; } = "";

    /// <summary>Kết luận của LLM sau lượt này: có cần hỏi xoáy tiếp không.</summary>
    public bool NeedFollowUp { get; set; }

    /// <summary>Lý do LLM đưa ra (chỉ để lưu log, không đọc cho sinh viên nghe).</summary>
    public string? Reason { get; set; }

    /// <summary>Các chunk tài liệu đã dùng làm căn cứ.</summary>
    public List<string> Sources { get; set; } = new();

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime EndedAt { get; set; } = DateTime.UtcNow;
}
