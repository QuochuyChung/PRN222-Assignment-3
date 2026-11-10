using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Models;

namespace AiInterview.Core.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default);
    Task<IReadOnlyList<VectorHit>> SearchAsync(float[] vector, int topK, VectorFilter? filter = null, CancellationToken ct = default);
    Task DeleteByDocumentAsync(string documentId, CancellationToken ct = default);
}

