using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AiInterview.Infrastructure.Repositories;

public class MongoDocumentEntity
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("size")]
    public long Size { get; set; }

    [BsonElement("storageKey")]
    public string StorageKey { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = DocumentStatus.Uploaded.ToString();

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("chunkCount")]
    public int ChunkCount { get; set; }

    [BsonElement("pageCount")]
    public int PageCount { get; set; }

    [BsonElement("uploadedBy")]
    public string UploadedBy { get; set; } = "Lecturer";

    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DocumentMetadata ToModel()
    {
        return new DocumentMetadata
        {
            Id = Id,
            FileName = FileName,
            ContentType = ContentType,
            Size = Size,
            StorageKey = StorageKey,
            Status = Enum.TryParse<DocumentStatus>(Status, out var st) ? st : DocumentStatus.Uploaded,
            ErrorMessage = ErrorMessage,
            ChunkCount = ChunkCount,
            PageCount = PageCount,
            UploadedBy = UploadedBy,
            UploadedAt = UploadedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public static MongoDocumentEntity FromModel(DocumentMetadata model)
    {
        return new MongoDocumentEntity
        {
            Id = model.Id,
            FileName = model.FileName,
            ContentType = model.ContentType,
            Size = model.Size,
            StorageKey = model.StorageKey,
            Status = model.Status.ToString(),
            ErrorMessage = model.ErrorMessage,
            ChunkCount = model.ChunkCount,
            PageCount = model.PageCount,
            UploadedBy = model.UploadedBy,
            UploadedAt = model.UploadedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}

public class MongoDocumentRepository : IDocumentRepository
{
    private readonly IMongoCollection<MongoDocumentEntity> _collection;
    private readonly ILogger<MongoDocumentRepository> _logger;

    public MongoDocumentRepository(
        IMongoDatabase database,
        IOptions<AiInterviewOptions> options,
        ILogger<MongoDocumentRepository> logger)
    {
        string collectionName = options.Value.Mongo?.CollectionName ?? "documents";
        _collection = database.GetCollection<MongoDocumentEntity>(collectionName);
        _logger = logger;
    }

    public async Task<DocumentMetadata?> GetByIdAsync(string documentId, CancellationToken ct = default)
    {
        try
        {
            var entity = await _collection.Find(d => d.Id == documentId).FirstOrDefaultAsync(ct);
            return entity?.ToModel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDocumentRepository.cs - MongoDB: Lấy tài liệu thất bại, documentId={DocId}", documentId);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentMetadata>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            var entities = await _collection
                .Find(FilterDefinition<MongoDocumentEntity>.Empty)
                .SortByDescending(d => d.UploadedAt)
                .ToListAsync(ct);

            return entities.Select(e => e.ToModel()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDocumentRepository.cs - MongoDB: Lấy danh sách tài liệu thất bại");
            throw;
        }
    }

    public async Task CreateAsync(DocumentMetadata document, CancellationToken ct = default)
    {
        try
        {
            var entity = MongoDocumentEntity.FromModel(document);
            await _collection.InsertOneAsync(entity, cancellationToken: ct);

            _logger.LogInformation("MongoDocumentRepository.cs - MongoDB: Thêm tài liệu mới thành công, documentId={DocId}, fileName={FileName}",
                document.Id, document.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDocumentRepository.cs - MongoDB: Thêm tài liệu thất bại, documentId={DocId}", document.Id);
            throw;
        }
    }

    public async Task UpdateStatusAsync(
        string documentId,
        DocumentStatus status,
        string? errorMessage = null,
        int? chunkCount = null,
        int? pageCount = null,
        CancellationToken ct = default)
    {
        try
        {
            var update = Builders<MongoDocumentEntity>.Update
                .Set(d => d.Status, status.ToString())
                .Set(d => d.ErrorMessage, errorMessage)
                .Set(d => d.UpdatedAt, DateTime.UtcNow);

            if (chunkCount.HasValue)
            {
                update = update.Set(d => d.ChunkCount, chunkCount.Value);
            }
            if (pageCount.HasValue)
            {
                update = update.Set(d => d.PageCount, pageCount.Value);
            }

            await _collection.UpdateOneAsync(d => d.Id == documentId, update, cancellationToken: ct);

            _logger.LogInformation("MongoDocumentRepository.cs - MongoDB: Cập nhật trạng thái thành công, documentId={DocId}, status={Status}",
                documentId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDocumentRepository.cs - MongoDB: Cập nhật trạng thái thất bại, documentId={DocId}, status={Status}",
                documentId, status);
            throw;
        }
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        try
        {
            await _collection.DeleteOneAsync(d => d.Id == documentId, ct);
            _logger.LogInformation("MongoDocumentRepository.cs - MongoDB: Xóa tài liệu thành công, documentId={DocId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDocumentRepository.cs - MongoDB: Xóa tài liệu thất bại, documentId={DocId}", documentId);
            throw;
        }
    }
}

