using System.Collections.Concurrent;
using AiInterview.Data.Entities;
using AiInterview.Data.Models;

namespace AiInterview.Data.Repositories.Documents;

/// <summary>Bản Fake trong bộ nhớ (singleton). Dữ liệu mất khi tắt app.</summary>
public sealed class FakeDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<string, DocumentRecord> _items = new();

    public Task CreateAsync(DocumentRecord document, CancellationToken ct = default)
    {
        _items[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<DocumentRecord?> GetAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);

    public Task<IReadOnlyList<DocumentRecord>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default)
    {
        IReadOnlyList<DocumentRecord> result = _items.Values
            .Where(d => status is null || d.Status == status)
            .OrderByDescending(d => d.UploadedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(DocumentStatus? status = null, CancellationToken ct = default)
        => Task.FromResult(_items.Values.Count(d => status is null || d.Status == status));

    public Task UpdateStatusAsync(
        string id,
        DocumentStatus status,
        string? errorMessage = null,
        int? chunkCount = null,
        int? pageCount = null,
        CancellationToken ct = default)
    {
        if (_items.TryGetValue(id, out var item))
        {
            item.Status = status;
            item.ErrorMessage = errorMessage;
            if (chunkCount is not null) item.ChunkCount = chunkCount.Value;
            if (pageCount is not null) item.PageCount = pageCount.Value;
            item.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
