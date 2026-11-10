namespace AiInterview.Core.Options;

public class AiInterviewOptions
{
    public const string SectionName = "AiInterview";

    public string InfraMode { get; set; } = "Fake"; // "Fake" or "Real"
    public string Role { get; set; } = "all"; // "web", "worker", or "all"
    public MinioOptions Minio { get; set; } = new();
    public MongoOptions Mongo { get; set; } = new();
    public RedisOptions Redis { get; set; } = new();
    public QdrantOptions Qdrant { get; set; } = new();
    public EmbeddingOptions Embedding { get; set; } = new();
    public ChunkingOptions Chunking { get; set; } = new();
}

public class MinioOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "ai-interview-docs";
    public bool ForcePathStyle { get; set; } = true;
    public bool UseSsl { get; set; } = false;
}

public class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "AiInterview";
    public string CollectionName { get; set; } = "documents";
}

public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string DocsStreamKey { get; set; } = "docs-stream";
    public string DocsConsumerGroup { get; set; } = "docs-workers";
}

public class QdrantOptions
{
    public string Endpoint { get; set; } = "http://localhost:6334";
    public string? ApiKey { get; set; }
    public string CollectionName { get; set; } = "chunks";
}

public class EmbeddingOptions
{
    public string ModelName { get; set; } = "text-embedding-004";
    public int Dimensions { get; set; } = 768;
    public string? ApiKey { get; set; }
    public int BatchSize { get; set; } = 16;
    public int MaxRetries { get; set; } = 3;
    public int InitialBackoffMs { get; set; } = 500;
}

public class ChunkingOptions
{
    public int ChunkSize { get; set; } = 500;
    public int ChunkOverlap { get; set; } = 100;
}

