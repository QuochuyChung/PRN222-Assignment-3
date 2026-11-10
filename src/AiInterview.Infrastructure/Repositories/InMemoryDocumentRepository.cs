using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Repositories;

public class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<string, DocumentMetadata> _docs = new();
    private readonly ILogger<InMemoryDocumentRepository> _logger;

    public InMemoryDocumentRepository(ILogger<InMemoryDocumentRepository> logger)
    {
        _logger = logger;
    }

    public Task<DocumentMetadata?> GetByIdAsync(string documentId, CancellationToken ct = default)
    {
        _docs.TryGetValue(documentId, out var doc);
        return Task.FromResult(doc);
    }

    public Task<IReadOnlyList<DocumentMetadata>> ListAsync(CancellationToken ct = default)
    {
        var list = _docs.Values.OrderByDescending(d => d.UploadedAt).ToList();
        return Task.FromResult<IReadOnlyList<DocumentMetadata>>(list);
    }

    public Task CreateAsync(DocumentMetadata document, CancellationToken ct = default)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _docs[document.Id] = document;
        _logger.LogInformation("InMemoryDocumentRepository.cs - App: Created in-memory document, documentId={DocId}, fileName={FileName}",
            document.Id, document.FileName);
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(
        string documentId,
        DocumentStatus status,
        string? errorMessage = null,
        int? chunkCount = null,
        int? pageCount = null,
        CancellationToken ct = default)
    {
        if (_docs.TryGetValue(documentId, out var doc))
        {
            doc.Status = status;
            doc.ErrorMessage = errorMessage;
            if (chunkCount.HasValue) doc.ChunkCount = chunkCount.Value;
            if (pageCount.HasValue) doc.PageCount = pageCount.Value;
            doc.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("InMemoryDocumentRepository.cs - App: Updated in-memory document status, documentId={DocId}, status={Status}",
                documentId, status);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        _docs.TryRemove(documentId, out _);
        _logger.LogInformation("InMemoryDocumentRepository.cs - App: Deleted in-memory document, documentId={DocId}", documentId);
        return Task.CompletedTask;
    }
}

