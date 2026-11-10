using System;

namespace AiInterview.Core.Models;

public class VectorPoint
{
    public string Id { get; set; } = string.Empty; // "{documentId}:{chunkIndex}"
    public string DocumentId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int Page { get; set; }
    public string Text { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
}

public class VectorHit
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int Page { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Score { get; set; }
}

public class VectorFilter
{
    public string? DocumentId { get; set; }
}

