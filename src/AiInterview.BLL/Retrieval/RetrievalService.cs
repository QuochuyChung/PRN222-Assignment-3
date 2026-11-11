using AiInterview.DAL.Models;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.DAL.Repositories.Vectors;

namespace AiInterview.BLL.Retrieval;

internal sealed class RetrievalService(IEmbeddingRepository embeddings, IVectorRepository vectors) : IRetrievalService
{
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(string query, RetrievalScope scope, int topK, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query is required.", nameof(query));
        if (topK < 1) throw new ArgumentOutOfRangeException(nameof(topK));
        ArgumentNullException.ThrowIfNull(scope);
        var embedded = await embeddings.EmbedAsync([query], ct);
        if (embedded.Count != 1) throw new InvalidOperationException("Embedding returned an unexpected number of vectors.");
        var filter = scope.DocumentIds is { Count: > 0 } ids ? new VectorFilter(ids) : null;
        var hits = await vectors.SearchAsync(embedded[0], topK, filter, ct);
        return hits.Select(h => new RetrievedChunk(h.Id, h.DocumentId, h.Page, h.Text, h.Score)).ToList();
    }
}
