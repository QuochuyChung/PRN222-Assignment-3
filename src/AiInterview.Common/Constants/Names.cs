namespace AiInterview.Common.Constants;

/// <summary>Tên stream và consumer group trên Redis (xem tasks.md, Phụ lục B).</summary>
public static class QueueNames
{
    public const string DocsStream = "docs-stream";
    public const string DocsGroup = "docs-workers";

    public const string TurnsStream = "turns-stream";
    public const string TurnsGroup = "sql-writers";

    /// <summary>Stream chứa message lỗi quá số lần thử: <c>{stream}-dead</c>.</summary>
    public static string DeadLetter(string stream) => $"{stream}-dead";
}

/// <summary>Quy ước key Redis (xem tasks.md, Phụ lục B).</summary>
public static class RedisKeys
{
    /// <summary>TTL của trạng thái phiên thi, gia hạn mỗi khi có hoạt động.</summary>
    public static readonly TimeSpan InterviewTtl = TimeSpan.FromHours(2);

    public static string Interview(string sessionId) => $"interview:{sessionId}";

    public static string Lock(string sessionId, string turnId) => $"lock:{sessionId}:{turnId}";
}

public static class HttpClientNames
{
    public const string Gemini = "gemini";
}
