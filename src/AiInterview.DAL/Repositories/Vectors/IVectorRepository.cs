using AiInterview.DAL.Models;

namespace AiInterview.DAL.Repositories.Vectors;

/// <summary>Chunk + embedding. Real = Qdrant (Dev A, ING-06). Upsert must be idempotent (fixed point id).</summary>
public interface IVectorRepository
{
    Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default);

    Task<IReadOnlyList<VectorHit>> SearchAsync(
        float[] vector,
        int topK,
        VectorFilter? filter = null,
        CancellationToken ct = default);

    Task DeleteByDocumentAsync(string documentId, CancellationToken ct = default);
}
