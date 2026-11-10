using System.Collections.Generic;

namespace AiInterview.Core.Models;

public class ExtractedDocument
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int PageCount => Pages.Count;
    public List<ExtractedPage> Pages { get; set; } = new();
}

public class ExtractedPage
{
    public int PageNumber { get; set; } // 1-based page or slide index
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
}

