using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AiInterview.Infrastructure.Vector;

public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly int _dimensions;
    private readonly ILogger<QdrantVectorStore> _logger;
    private bool _collectionInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public QdrantVectorStore(
        QdrantClient client,
        IOptions<AiInterviewOptions> options,
        ILogger<QdrantVectorStore> logger)
    {
        _client = client;
        _options = options.Value.Qdrant ?? new QdrantOptions();
        _dimensions = options.Value.Embedding?.Dimensions > 0 ? options.Value.Embedding.Dimensions : 768;
        _logger = logger;
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken ct)
    {
        if (_collectionInitialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_collectionInitialized) return;

            bool exists = await _client.CollectionExistsAsync(_options.CollectionName, ct);
            if (!exists)
            {
                await _client.CreateCollectionAsync(
                    _options.CollectionName,
                    new VectorParams { Size = (ulong)_dimensions, Distance = Distance.Cosine },
                    cancellationToken: ct);

                _logger.LogInformation("QdrantVectorStore.cs - Qdrant: Khởi tạo collection thành công, collection={Collection}, số chiều={Dimensions}",
                    _options.CollectionName, _dimensions);
            }

            _collectionInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore.cs - Qdrant: Lỗi khi kiểm tra/tạo collection={Collection}",
                _options.CollectionName);
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0) return;

        await EnsureCollectionExistsAsync(ct);

        string docId = points[0].DocumentId;
        try
        {
            var pointStructs = points.Select(p =>
            {
                // Generate deterministic Guid from point ID "{documentId}:{chunkIndex}"
                Guid pointGuid = DeterministicGuid(p.Id);
                var point = new PointStruct
                {
                    Id = pointGuid,
                    Vectors = p.Vector
                };

                point.Payload.Add("id", p.Id);
                point.Payload.Add("documentId", p.DocumentId);
                point.Payload.Add("chunkIndex", p.ChunkIndex);
                point.Payload.Add("page", p.Page);
                point.Payload.Add("text", p.Text);

                return point;
            }).ToList();

            await _client.UpsertAsync(_options.CollectionName, pointStructs, cancellationToken: ct);

            _logger.LogInformation("QdrantVectorStore.cs - Qdrant: Upsert thành công, collection={Collection}, documentId={DocId}, số điểm={Count}",
                _options.CollectionName, docId, points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore.cs - Qdrant: Upsert thất bại, collection={Collection}, documentId={DocId}, số điểm={Count}",
                _options.CollectionName, docId, points.Count);
            throw;
        }
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(float[] vector, int topK, VectorFilter? filter = null, CancellationToken ct = default)
    {
        await EnsureCollectionExistsAsync(ct);

        try
        {
            Filter? qdrantFilter = null;
            if (filter != null && !string.IsNullOrEmpty(filter.DocumentId))
            {
                qdrantFilter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "documentId",
                                Match = new Match { Keyword = filter.DocumentId }
                            }
                        }
                    }
                };
            }

            var scoredPoints = await _client.SearchAsync(
                _options.CollectionName,
                vector,
                filter: qdrantFilter,
                limit: (ulong)topK,
                cancellationToken: ct);

            var hits = scoredPoints.Select(sp => new VectorHit
            {
                Id = sp.Payload.TryGetValue("id", out var idVal) ? idVal.StringValue : sp.Id.ToString(),
                DocumentId = sp.Payload.TryGetValue("documentId", out var docIdVal) ? docIdVal.StringValue : string.Empty,
                ChunkIndex = sp.Payload.TryGetValue("chunkIndex", out var idxVal) ? (int)idxVal.IntegerValue : 0,
                Page = sp.Payload.TryGetValue("page", out var pageVal) ? (int)pageVal.IntegerValue : 0,
                Text = sp.Payload.TryGetValue("text", out var textVal) ? textVal.StringValue : string.Empty,
                Score = sp.Score
            }).ToList();

            _logger.LogInformation("QdrantVectorStore.cs - Qdrant: Tìm kiếm thành công, collection={Collection}, kết quả={Count}",
                _options.CollectionName, hits.Count);

            return hits;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore.cs - Qdrant: Tìm kiếm thất bại, collection={Collection}",
                _options.CollectionName);
            throw;
        }
    }

    public async Task DeleteByDocumentAsync(string documentId, CancellationToken ct = default)
    {
        await EnsureCollectionExistsAsync(ct);

        try
        {
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "documentId",
                            Match = new Match { Keyword = documentId }
                        }
                    }
                }
            };

            await _client.DeleteAsync(_options.CollectionName, filter, cancellationToken: ct);

            _logger.LogInformation("QdrantVectorStore.cs - Qdrant: Xóa vector theo documentId thành công, collection={Collection}, documentId={DocId}",
                _options.CollectionName, documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QdrantVectorStore.cs - Qdrant: Xóa vector theo documentId thất bại, collection={Collection}, documentId={DocId}",
                _options.CollectionName, documentId);
            throw;
        }
    }

    private static Guid DeterministicGuid(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}

