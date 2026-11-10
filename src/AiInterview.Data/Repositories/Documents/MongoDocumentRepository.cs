using AiInterview.Common.Configuration;
using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.Data.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace AiInterview.Data.Repositories.Documents;

/// <summary>Document metadata in the MongoDB collection <c>documents</c> (camelCase fields, status stored as string).</summary>
public sealed class MongoDocumentRepository : IDocumentRepository
{
    private const string File = "MongoDocumentRepository.cs";
    private const string CollectionName = "documents";
    private readonly IMongoCollection<DocumentRecord> _collection;
    private readonly ILogger<MongoDocumentRepository> _logger;

    static MongoDocumentRepository()
    {
        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register("documents", pack, t => t == typeof(DocumentRecord));
    }

    public MongoDocumentRepository(IMongoDatabase database, ILogger<MongoDocumentRepository> logger)
    {
        _collection = database.GetCollection<DocumentRecord>(CollectionName);
        _logger = logger;
    }

    public Task CreateAsync(DocumentRecord document, CancellationToken ct = default)
        => Run("Insert", document.Id, () => _collection.InsertOneAsync(document, cancellationToken: ct));

    public async Task<DocumentRecord?> GetAsync(string id, CancellationToken ct = default)
    {
        DocumentRecord? result = null;
        await Run("Get", id, async () => result = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(ct), logSuccess: false);
        return result;
    }

    public async Task<IReadOnlyList<DocumentRecord>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default)
    {
        List<DocumentRecord> result = [];
        await Run("List", status?.ToString() ?? "all", async () =>
            result = await _collection.Find(StatusFilter(status)).SortByDescending(d => d.UploadedAt).ToListAsync(ct), logSuccess: false);
        return result;
    }

    public async Task<int> CountAsync(DocumentStatus? status = null, CancellationToken ct = default)
    {
        long count = 0;
        await Run("Count", status?.ToString() ?? "all", async () =>
            count = await _collection.CountDocumentsAsync(StatusFilter(status), cancellationToken: ct), logSuccess: false);
        return (int)count;
    }

    public Task UpdateStatusAsync(
        string id,
        DocumentStatus status,
        string? errorMessage = null,
        int? chunkCount = null,
        int? pageCount = null,
        CancellationToken ct = default)
    {
        var update = Builders<DocumentRecord>.Update
            .Set(d => d.Status, status)
            .Set(d => d.ErrorMessage, errorMessage)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        if (chunkCount is not null) update = update.Set(d => d.ChunkCount, chunkCount.Value);
        if (pageCount is not null) update = update.Set(d => d.PageCount, pageCount.Value);

        return Run($"Update status to {status}", id, () => _collection.UpdateOneAsync(d => d.Id == id, update, cancellationToken: ct));
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => Run("Delete", id, () => _collection.DeleteOneAsync(d => d.Id == id, ct));

    private static FilterDefinition<DocumentRecord> StatusFilter(DocumentStatus? status)
        => status is null ? Builders<DocumentRecord>.Filter.Empty : Builders<DocumentRecord>.Filter.Eq(d => d.Status, status.Value);

    private async Task Run(string action, string id, Func<Task> call, bool logSuccess = true)
    {
        try
        {
            await call();
            if (logSuccess) _logger.Info(File, ServiceNames.MongoDB, "{Action} succeeded, documentId={DocId}", action, id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, File, ServiceNames.MongoDB, "{Action} failed, documentId={DocId}", action, id);
            throw;
        }
    }
}
