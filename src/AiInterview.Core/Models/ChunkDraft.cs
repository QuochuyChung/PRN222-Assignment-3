using System.Collections.Generic;

namespace AiInterview.Core.Models;

public class ChunkDraft
{
    public string DocumentId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int Page { get; set; }
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

