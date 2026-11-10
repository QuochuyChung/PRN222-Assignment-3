namespace AiInterview.Common.Configuration;

// Các class option bên dưới chỉ chứa cấu hình, KHÔNG chứa secret mặc định.
// Giá trị thật đến từ biến môi trường (ví dụ Mongo__ConnectionString) / appsettings; secret nằm ở Vault.

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "";

    public string Database { get; set; } = "aiinterview";
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "";
}

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";

    /// <summary>Cổng gRPC (mặc định 6334).</summary>
    public int Port { get; set; } = 6334;

    public bool UseHttps { get; set; }

    public string? ApiKey { get; set; }

    public string Collection { get; set; } = "chunks";
}

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    /// <summary>Ví dụ: http://host:9000</summary>
    public string Endpoint { get; set; } = "";

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    public string Bucket { get; set; } = "ai-interview-docs";
}

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";

    public string ApiKey { get; set; } = "";

    /// <summary>Model sinh văn bản. Lấy tên chính xác từ Google AI Studio, không hard-code trong code.</summary>
    public string Model { get; set; } = "";

    /// <summary>Model embedding (khác model sinh văn bản).</summary>
    public string EmbeddingModel { get; set; } = "";

    /// <summary>Số chiều vector, phải khớp với collection trong Qdrant.</summary>
    public int EmbeddingDimensions { get; set; } = 768;
}

public sealed class InterviewOptions
{
    public const string SectionName = "Interview";

    public int QuestionCount { get; set; } = 3;

    public int MaxFollowUps { get; set; } = 2;

    public int AnswerSeconds { get; set; } = 60;
}
