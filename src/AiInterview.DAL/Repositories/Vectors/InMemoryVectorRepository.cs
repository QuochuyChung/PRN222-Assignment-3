using System.Collections.Concurrent;
using AiInterview.DAL.Models;

namespace AiInterview.DAL.Repositories.Vectors;

/// <summary>Fake vector store (singleton): brute-force cosine search over an in-memory dictionary keyed by point id.</summary>
public sealed class InMemoryVectorRepository : IVectorRepository
{
    private readonly ConcurrentDictionary<string, VectorPoint> _points = new();

    public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
    {
        foreach (var point in points) _points[point.Id] = point;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorHit>> SearchAsync(float[] vector, int topK, VectorFilter? filter = null, CancellationToken ct = default)
    {
        var ids = filter?.DocumentIds;
        IReadOnlyList<VectorHit> hits = _points.Values
            .Where(p => ids is null || ids.Contains(p.DocumentId))
            .Select(p => new VectorHit(p.Id, Cosine(vector, p.Vector), p.DocumentId, p.ChunkIndex, p.Page, p.Text))
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();
        return Task.FromResult(hits);
    }

    public Task DeleteByDocumentAsync(string documentId, CancellationToken ct = default)
    {
        foreach (var key in _points.Where(p => p.Value.DocumentId == documentId).Select(p => p.Key).ToList())
            _points.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>Number of stored points. Used by tests.</summary>
    public int Count => _points.Count;

    private static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0f : (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}
