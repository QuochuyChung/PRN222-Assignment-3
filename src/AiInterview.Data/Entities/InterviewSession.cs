namespace AiInterview.Data.Entities;

public class InterviewSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";

    public string StudentName { get; set; } = "";

    public SessionStatus Status { get; set; } = SessionStatus.Created;

    /// <summary>Số câu hỏi chính.</summary>
    public int QuestionCount { get; set; } = 3;

    /// <summary>Số lượt hỏi xoáy tối đa mỗi câu.</summary>
    public int MaxFollowUps { get; set; } = 2;

    /// <summary>Thời gian trả lời mỗi câu (giây).</summary>
    public int AnswerSeconds { get; set; } = 60;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<InterviewTurn> Turns { get; set; } = new();
}
