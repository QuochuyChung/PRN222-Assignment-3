using AiInterview.Common.Constants;
using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.Data.Models;
using AiInterview.Data.Repositories.Documents;
using AiInterview.Data.Repositories.Files;
using AiInterview.Data.Repositories.Queue;
using AiInterview.Data.Repositories.Vectors;
using AiInterview.Services.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Services.Documents;

internal sealed class DocumentService(
    IFileRepository files,
    IDocumentRepository documents,
    IVectorRepository vectors,
    IMessageQueueRepository queue,
    IDocumentProgressNotifier notifier,
    IOptions<IngestionOptions> options,
    ILogger<DocumentService> logger) : IDocumentService
{
    private const string File = "DocumentService.cs";

    public async Task<DocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken ct = default)
    {
        var fileName = SafeFileName(request.FileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var maxBytes = options.Value.MaxFileSizeMb * 1024L * 1024L;

        if (!IngestionOptions.AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not supported. Upload a .pdf, .docx or .pptx file.");
        if (request.Size <= 0)
            throw new ArgumentException("The file is empty.");
        if (request.Size > maxBytes)
            throw new ArgumentException($"The file is larger than {options.Value.MaxFileSizeMb} MB.");

        var record = new DocumentRecord
        {
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            Size = request.Size,
            Status = DocumentStatus.Processing,
            UploadedBy = request.UploadedBy
        };
        record.StorageKey = $"{record.Id}/{fileName}";

        // Required order: file -> metadata (Processing) -> docs-stream.
        await files.SaveAsync(record.StorageKey, request.Content, record.ContentType, ct);
        await documents.CreateAsync(record, ct);
        await EnqueueAsync(record, ct);

        logger.Info(File, ServiceNames.App, "Uploaded and queued, documentId={DocId}, fileName={FileName}, size={Size}B",
            record.Id, fileName, record.Size);
        await notifier.NotifyAsync(new DocumentProgress(record.Id, DocumentStatus.Processing, 5, "Queued"), ct);
        return record.ToDto();
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default)
        => (await documents.ListAsync(status, ct)).Select(d => d.ToDto()).ToList();

    public async Task<DocumentDto?> GetAsync(string id, CancellationToken ct = default)
        => (await documents.GetAsync(id, ct))?.ToDto();

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var record = await documents.GetAsync(id, ct) ?? throw new KeyNotFoundException("Document not found.");

        // Vectors and file first: if one of them fails the metadata stays, so the user can simply delete again.
        await vectors.DeleteByDocumentAsync(id, ct);
        await files.DeleteAsync(record.StorageKey, ct);
        await documents.DeleteAsync(id, ct);

        logger.Info(File, ServiceNames.App, "Deleted metadata, vectors and file, documentId={DocId}, key={Key}", id, record.StorageKey);
        await notifier.NotifyDeletedAsync(id, ct);
    }

    public async Task ReindexAsync(string id, CancellationToken ct = default)
    {
        var record = await documents.GetAsync(id, ct) ?? throw new KeyNotFoundException("Document not found.");
        if (record.Status == DocumentStatus.Processing)
            throw new InvalidOperationException("This document is already being processed.");

        await vectors.DeleteByDocumentAsync(id, ct);
        await documents.UpdateStatusAsync(id, DocumentStatus.Processing, null, 0, 0, ct);
        await EnqueueAsync(record, ct);

        logger.Info(File, ServiceNames.App, "Re-index queued, documentId={DocId}", id);
        await notifier.NotifyAsync(new DocumentProgress(id, DocumentStatus.Processing, 5, "Queued"), ct);
    }

    private async Task EnqueueAsync(DocumentRecord record, CancellationToken ct)
    {
        try
        {
            await queue.PublishAsync(QueueNames.DocsStream,
                new Dictionary<string, string> { ["documentId"] = record.Id, ["storageKey"] = record.StorageKey }, ct);
        }
        catch (Exception ex)
        {
            // Without a stream message nobody would ever pick it up: do not leave it stuck in Processing.
            logger.Error(ex, File, ServiceNames.App, "Could not queue document, documentId={DocId}", record.Id);
            await documents.UpdateStatusAsync(record.Id, DocumentStatus.Failed, "Could not queue the document for processing. Try re-index.", ct: CancellationToken.None);
            throw;
        }
    }

    /// <summary>Drops any client-side path and characters that are not allowed in a file name / storage key.</summary>
    private static string SafeFileName(string name)
    {
        var clean = Path.GetFileName(name.Replace('\\', '/'));
        clean = string.Concat(clean.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && !char.IsControl(c))).Trim();
        return clean.Length == 0 ? "document" : clean[..Math.Min(clean.Length, 200)];
    }
}
