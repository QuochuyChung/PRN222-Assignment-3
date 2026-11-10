using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Models;

namespace AiInterview.Core.Interfaces;

public interface IDocumentService
{
    Task<DocumentMetadata> UploadDocumentAsync(Stream content, string fileName, string contentType, long size, CancellationToken ct = default);
    Task ProcessDocumentAsync(string documentId, string storageKey, CancellationToken ct = default);
    Task DeleteDocumentAsync(string documentId, CancellationToken ct = default);
    Task ReindexDocumentAsync(string documentId, CancellationToken ct = default);
}

