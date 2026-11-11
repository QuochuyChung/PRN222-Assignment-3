using System.Security.Cryptography;
using System.Text;
using AiInterview.Common.Configuration;
using AiInterview.Common.Logging;
using AiInterview.DAL.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace AiInterview.DAL.Repositories.Vectors;

/// <summary>
/// Chunk vectors in Qdrant. Qdrant only accepts UUID/integer ids, so the logical id <c>{documentId}:{chunkIndex}</c>
/// is hashed into a stable UUID and also kept in the payload as <c>id</c>. Same input = same point, so upsert is idempotent.
/// </summary>
public sealed class QdrantVectorRepository(
    QdrantClient client,
    IOptions<QdrantOptions> qdrant,
    IOptions<GeminiOptions> gemini,
    ILogger<QdrantVectorRepository> logger) : IVectorRepository
{
    private const string File = "QdrantVectorRepository.cs";
    private readonly string _collection = qdrant.Value.Collection;
    private readonly ulong _dimensions = (ulong)gemini.Value.EmbeddingDimensions;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _ready;

    public async Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0) return;
        await EnsureCollectionAsync(ct);
        var docId = points[0].DocumentId;
        try
        {
            var structs = points.Select(p => new PointStruct
            {
                Id = PointGuid(p.Id),
                Vectors = p.Vector,
                Payload =
                {
                    ["id"] = p.Id,
                    ["documentId"] = p.DocumentId,
                    ["chunkIndex"] = p.ChunkIndex,
                    ["page"] = p.Page,
                    ["text"] = p.Text
                }
            }).ToList();
            await client.UpsertAsync(_collection, structs, cancellationToken: ct);
            logger.Info(File, ServiceNames.Qdrant, "Upsert succeeded, collection={Collection}, documentId={DocId}, points={Count}",
                _collection, docId, points.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Qdrant, "Upsert failed, collection={Collection}, documentId={DocId}, points={Count}",
                _collection, docId, points.Count);
            throw;
        }
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(float[] vector, int topK, VectorFilter? filter = null, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);
        try
        {
            Filter? qdrantFilter = filter?.DocumentIds is { Count: > 0 } ids ? new Filter { Must = { Match("documentId", ids.ToList()) } } : null;
            var result = await client.QueryAsync(_collection, query: vector, filter: qdrantFilter, limit: (ulong)topK, payloadSelector: true, cancellationToken: ct);
            logger.Info(File, ServiceNames.Qdrant, "Search succeeded, collection={Collection}, hits={Count}", _collection, result.Count);
            return result.Select(p => new VectorHit(
                p.Payload["id"].StringValue,
                p.Score,
                p.Payload["documentId"].StringValue,
                (int)p.Payload["chunkIndex"].IntegerValue,
                (int)p.Payload["page"].IntegerValue,
                p.Payload["text"].StringValue)).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Qdrant, "Search failed, collection={Collection}, topK={TopK}", _collection, topK);
            throw;
        }
    }

    public async Task DeleteByDocumentAsync(string documentId, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);
        try
        {
            await client.DeleteAsync(_collection, MatchKeyword("documentId", documentId), cancellationToken: ct);
            logger.Info(File, ServiceNames.Qdrant, "Deleted points, collection={Collection}, documentId={DocId}", _collection, documentId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Qdrant, "Delete failed, collection={Collection}, documentId={DocId}", _collection, documentId);
            throw;
        }
    }

    private static Guid PointGuid(string id) => new(MD5.HashData(Encoding.UTF8.GetBytes(id)));

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        if (_ready) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_ready) return;
            if (!await client.CollectionExistsAsync(_collection, ct))
            {
                await client.CreateCollectionAsync(_collection, new VectorParams { Size = _dimensions, Distance = Distance.Cosine }, cancellationToken: ct);
                await client.CreatePayloadIndexAsync(_collection, "documentId", PayloadSchemaType.Keyword, cancellationToken: ct);
                logger.Info(File, ServiceNames.Qdrant, "Created collection, collection={Collection}, dimensions={Dimensions}", _collection, _dimensions);
            }
            _ready = true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Qdrant, "Could not check or create collection, collection={Collection}", _collection);
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
