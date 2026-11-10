using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.Data.Models;
using AiInterview.Data.Repositories.Ai;
using AiInterview.Data.Repositories.Documents;
using AiInterview.Data.Repositories.Files;
using AiInterview.Data.Repositories.Vectors;
using Microsoft.Extensions.Logging;

namespace AiInterview.Services.Documents;

/// <summary>
/// extract -> chunk -> embed -> upsert -> Success. A file that can never work (scan, broken, empty) is marked Failed
/// right away; any other exception bubbles up so <see cref="DocumentWorker"/> can retry it.
/// </summary>
internal sealed class DocumentProcessingService(
    IDocumentRepository documents,
    IFileRepository files,
    IDocumentTextExtractor extractor,
    IChunker chunker,
    IEmbeddingRepository embeddings,
    IVectorRepository vectors,
    IDocumentProgressNotifier notifier,
    ILogger<DocumentProcessingService> logger) : IDocumentProcessingService
{
    private const string File = "DocumentProcessingService.cs";

    public async Task ProcessAsync(string documentId, CancellationToken ct = default)
    {
        var record = await documents.GetAsync(documentId, ct);
        if (record is null)
        {
            logger.Warn(File, ServiceNames.App, "Document no longer exists, skipping, documentId={DocId}", documentId);
            return;
        }

        await Progress(documentId, 15, "Reading file", ct);
        ExtractedDocument extracted;
        try
        {
            await using var stream = await files.OpenReadAsync(record.StorageKey, ct);
            extracted = await extractor.ExtractAsync(stream, record.FileName, ct);
        }
        catch (DocumentRejectedException ex)
        {
            await FailAsync(documentId, ex.Message, ct);
            return;
        }

        await Progress(documentId, 40, "Splitting into chunks", ct);
        var chunks = chunker.Chunk(extracted);
        if (chunks.Count == 0)
        {
            await FailAsync(documentId, "No readable text was found in this document.", ct);
            return;
        }

        await Progress(documentId, 60, $"Embedding {chunks.Count} chunks", ct);
        var vectorsOut = await embeddings.EmbedAsync(chunks.Select(c => c.Text).ToList(), ct);

        await Progress(documentId, 85, "Saving vectors", ct);
        var points = chunks.Select((c, i) => new VectorPoint($"{documentId}:{c.ChunkIndex}", vectorsOut[i], documentId, c.ChunkIndex, c.Page, c.Text)).ToList();
        await vectors.UpsertAsync(points, ct);

        var pageCount = extracted.Pages.Count;
        await documents.UpdateStatusAsync(documentId, DocumentStatus.Success, null, chunks.Count, pageCount, ct);
        logger.Info(File, ServiceNames.App, "Processed, documentId={DocId}, pages={Pages}, chunks={Chunks}", documentId, pageCount, chunks.Count);
        await notifier.NotifyAsync(new DocumentProgress(documentId, DocumentStatus.Success, 100, "Done", null, chunks.Count, pageCount), ct);
    }

    private Task Progress(string documentId, int percent, string step, CancellationToken ct)
        => notifier.NotifyAsync(new DocumentProgress(documentId, DocumentStatus.Processing, percent, step), ct);

    private async Task FailAsync(string documentId, string reason, CancellationToken ct)
    {
        logger.Warn(File, ServiceNames.App, "Marked Failed without retry, documentId={DocId}, reason={Reason}", documentId, reason);
        await documents.UpdateStatusAsync(documentId, DocumentStatus.Failed, reason, 0, 0, ct);
        await notifier.NotifyAsync(new DocumentProgress(documentId, DocumentStatus.Failed, 100, "Failed", reason), ct);
    }
}
