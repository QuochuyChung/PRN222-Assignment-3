using Microsoft.Extensions.Options;

namespace AiInterview.Services.Documents;

/// <summary>
/// Sliding window per page/slide/section, so a chunk never spans two pages and keeps its page number.
/// Cuts at a sentence or line break when possible, otherwise at a space. Chunk indexes are global and start at 0.
/// </summary>
internal sealed class Chunker(IOptions<IngestionOptions> options) : IChunker
{
    public IReadOnlyList<ChunkDraft> Chunk(ExtractedDocument document)
    {
        var size = Math.Max(200, options.Value.ChunkSize);
        var overlap = Math.Clamp(options.Value.ChunkOverlap, 0, size / 2);
        var chunks = new List<ChunkDraft>();

        foreach (var page in document.Pages)
        {
            var text = page.Text.Trim();
            var start = 0;
            while (start < text.Length)
            {
                var end = Math.Min(start + size, text.Length);
                if (end < text.Length) end = BestCut(text, start + size / 2, end);

                var piece = text[start..end].Trim();
                if (piece.Length > 0) chunks.Add(new ChunkDraft(chunks.Count, page.Index, piece));
                if (end >= text.Length) break;

                var next = end - overlap;
                var space = text.IndexOf(' ', next);
                start = space >= 0 && space < end ? space + 1 : Math.Max(next, start + 1);
            }
        }

        return chunks;
    }

    /// <summary>Last sentence end / line break in [min, max), else last space, else max.</summary>
    private static int BestCut(string text, int min, int max)
    {
        for (var i = max - 1; i >= min; i--)
            if (text[i] == '\n' || (".!?".Contains(text[i]) && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1])))
                return i + 1;
        var space = text.LastIndexOf(' ', max - 1, max - min);
        return space > min ? space : max;
    }
}
