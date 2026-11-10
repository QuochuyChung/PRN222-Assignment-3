using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Vector;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, VectorPoint> _points = new();
    private readonly ILogger<InMemoryVectorStore> _logger;

    public InMemoryVectorStore(ILogger<InMemoryVectorStore> logger)
    {
        _logger = logger;
    }

    public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
    {
        foreach (var point in points)
        {
            _points[point.Id] = point;
        }

        string docId = points.FirstOrDefault()?.DocumentId ?? string.Empty;
        _logger.LogInformation("InMemoryVectorStore.cs - App: Upsert vector giả lập thành công, documentId={DocId}, số điểm={Count}",
            docId, points.Count);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorHit>> SearchAsync(float[] vector, int topK, VectorFilter? filter = null, CancellationToken ct = default)
    {
        var candidates = _points.Values.AsEnumerable();

        if (filter != null && !string.IsNullOrEmpty(filter.DocumentId))
        {
            candidates = candidates.Where(p => p.DocumentId == filter.DocumentId);
        }

        var hits = candidates
            .Select(p => new VectorHit
            {
                Id = p.Id,
                DocumentId = p.DocumentId,
                ChunkIndex = p.ChunkIndex,
                Page = p.Page,
                Text = p.Text,
                Score = ComputeCosineSimilarity(vector, p.Vector)
            })
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorHit>>(hits);
    }

    public Task DeleteByDocumentAsync(string documentId, CancellationToken ct = default)
    {
        var keysToRemove = _points.Where(kvp => kvp.Value.DocumentId == documentId)
                                  .Select(kvp => kvp.Key)
                                  .ToList();

        foreach (var key in keysToRemove)
        {
            _points.TryRemove(key, out _);
        }

        _logger.LogInformation("InMemoryVectorStore.cs - App: Xóa vector theo tài liệu thành công, documentId={DocId}, số điểm xóa={Count}",
            documentId, keysToRemove.Count);

        return Task.CompletedTask;
    }

    private static float ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;

        float dot = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denom > 0f ? (dot / denom) : 0f;
    }
}

