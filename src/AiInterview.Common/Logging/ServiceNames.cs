namespace AiInterview.Common.Logging;

/// <summary>Tên service dùng trong log theo quy ước: <c>&lt;Tên file&gt; - &lt;Service&gt;: &lt;Mô tả&gt;</c>.</summary>
public static class ServiceNames
{
    public const string SqlServer = "SqlServer";
    public const string MongoDB = "MongoDB";
    public const string Qdrant = "Qdrant";
    public const string Redis = "Redis";
    public const string MinIO = "MinIO";
    public const string Gemini = "Gemini";
    public const string SignalR = "SignalR";

    /// <summary>Logic nội bộ của ứng dụng (không gọi hạ tầng ngoài).</summary>
    public const string App = "App";
}
