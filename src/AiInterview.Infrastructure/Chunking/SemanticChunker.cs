using System;
using System.Collections.Generic;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Infrastructure.Chunking;

public class SemanticChunker : IChunker
{
    private readonly ChunkingOptions _options;
    private readonly ILogger<SemanticChunker> _logger;

    public SemanticChunker(
        IOptions<AiInterviewOptions> options,
        ILogger<SemanticChunker> logger)
    {
        _options = options.Value.Chunking ?? new ChunkingOptions();
        _logger = logger;
    }

    public IReadOnlyList<ChunkDraft> Chunk(ExtractedDocument doc)
    {
        var chunks = new List<ChunkDraft>();
        int chunkIndex = 0;
        int chunkSize = Math.Max(100, _options.ChunkSize);
        int overlap = Math.Clamp(_options.ChunkOverlap, 0, chunkSize / 2);
        int step = chunkSize - overlap;

        foreach (var page in doc.Pages)
        {
            string content = page.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content)) continue;

            if (content.Length <= chunkSize)
            {
                int currentIdx = chunkIndex++;
                chunks.Add(new ChunkDraft
                {
                    DocumentId = doc.DocumentId,
                    ChunkIndex = currentIdx,
                    Page = page.PageNumber,
                    Text = content,
                    Metadata = new Dictionary<string, string>
                    {
                        ["page"] = page.PageNumber.ToString(),
                        ["title"] = page.Title ?? string.Empty,
                        ["chunk_index"] = currentIdx.ToString(),
                        ["length"] = content.Length.ToString()
                    }
                });
            }
            else
            {
                // Sliding window chunking
                int start = 0;
                while (start < content.Length)
                {
                    int length = Math.Min(chunkSize, content.Length - start);
                    string chunkText = content.Substring(start, length).Trim();

                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        int currentIdx = chunkIndex++;
                        chunks.Add(new ChunkDraft
                        {
                            DocumentId = doc.DocumentId,
                            ChunkIndex = currentIdx,
                            Page = page.PageNumber,
                            Text = chunkText,
                            Metadata = new Dictionary<string, string>
                            {
                                ["page"] = page.PageNumber.ToString(),
                                ["title"] = page.Title ?? string.Empty,
                                ["chunk_index"] = currentIdx.ToString(),
                                ["length"] = chunkText.Length.ToString()
                            }
                        });
                    }

                    start += step;
                }
            }
        }

        _logger.LogInformation("SemanticChunker.cs - App: Chunking complete, documentId={DocId}, chunks={Count}, chunkSize={ChunkSize}, overlap={Overlap}",
            doc.DocumentId, chunks.Count, chunkSize, overlap);

        return chunks;
    }
}

