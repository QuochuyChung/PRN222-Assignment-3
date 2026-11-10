namespace AiInterview.Data.Entities;

public enum DocumentStatus
{
    Uploaded,
    Processing,
    Success,
    Failed
}

public enum QuestionStatus
{
    Draft,
    Approved,
    Rejected
}

public enum SessionStatus
{
    Created,
    InProgress,
    Completed,
    Abandoned
}

public enum BloomLevel
{
    Remember,
    Understand,
    Apply,
    Analyze
}

/// <summary>Trạng thái của state machine phiên thi (lưu ở Redis). Dev C có thể tinh chỉnh.</summary>
public enum InterviewState
{
    AskQuestion,
    Listening,
    Evaluating,
    FollowUp,
    NextQuestion,
    Finished
}
