using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Models;

namespace AiInterview.Core.Interfaces;

public interface IDocumentRepository
{
    Task<DocumentMetadata?> GetByIdAsync(string documentId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentMetadata>> ListAsync(CancellationToken ct = default);
    Task CreateAsync(DocumentMetadata document, CancellationToken ct = default);
    Task UpdateStatusAsync(
        string documentId,
        DocumentStatus status,
        string? errorMessage = null,
        int? chunkCount = null,
        int? pageCount = null,
        CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
}

