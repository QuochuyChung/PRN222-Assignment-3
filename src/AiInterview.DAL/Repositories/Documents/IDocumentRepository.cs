using AiInterview.DAL.Entities;
using AiInterview.Common.Enums;
using AiInterview.DAL.Models;

namespace AiInterview.DAL.Repositories.Documents;

/// <summary>Document metadata. Real = MongoDB (Dev A, ING-02). Fake = in-memory.</summary>
public interface IDocumentRepository
{
    Task CreateAsync(DocumentRecord document, CancellationToken ct = default);

    Task<DocumentRecord?> GetAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRecord>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default);

    Task<int> CountAsync(DocumentStatus? status = null, CancellationToken ct = default);

    Task UpdateStatusAsync(
        string id,
        DocumentStatus status,
        string? errorMessage = null,
        int? chunkCount = null,
        int? pageCount = null,
        CancellationToken ct = default);

    Task DeleteAsync(string id, CancellationToken ct = default);
}
