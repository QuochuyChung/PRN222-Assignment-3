using AiInterview.Common.Enums;

namespace AiInterview.BLL.Documents;

/// <summary>Document returned to the UI. Does not expose internal details such as the storage key.</summary>
public sealed record DocumentDto(
    string Id,
    string FileName,
    string ContentType,
    long Size,
    DocumentStatus Status,
    string? ErrorMessage,
    int ChunkCount,
    int PageCount,
    string? UploadedBy,
    DateTime UploadedAt,
    DateTime UpdatedAt);

public sealed record UploadDocumentRequest(string FileName, string ContentType, Stream Content, long Size, string? UploadedBy);

public sealed record ExtractedPage(int Index, string Text);

public sealed record ExtractedDocument(string FileName, IReadOnlyList<ExtractedPage> Pages);

public sealed record ChunkDraft(int ChunkIndex, int Page, string Text);

/// <summary>
/// Document management for the upload/list page. Dev A (ING-02, ING-08).
/// Upload order: save the file (IFileRepository) -> write Processing metadata (IDocumentRepository) -> push to docs-stream.
/// Returns <see cref="DocumentDto"/>, never a DAL model.
/// </summary>
public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default);

    Task<DocumentDto?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Deletes metadata, vectors and the original file in one call.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Runs the pipeline again (for example after changing the embedding model).</summary>
    Task ReindexAsync(string id, CancellationToken ct = default);
}

/// <summary>Extracts text from PPTX/PDF/DOCX. Dev A (ING-03). A scanned PDF without text must fail with a clear message.</summary>
public interface IDocumentTextExtractor
{
    Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}

/// <summary>Splits text into chunks and keeps the page/slide number. Dev A (ING-04).</summary>
public interface IChunker
{
    IReadOnlyList<ChunkDraft> Chunk(ExtractedDocument document);
}

/// <summary>Processing pipeline for one document: extract, chunk, embed, store vectors, update status. Dev A (ING-07).</summary>
public interface IDocumentProcessingService
{
    Task ProcessAsync(string documentId, CancellationToken ct = default);
}

/// <summary>Live progress of one document, pushed to the UI (SignalR in Web). <see cref="Percent"/> is 0-100.</summary>
public sealed record DocumentProgress(
    string DocumentId,
    DocumentStatus Status,
    int Percent,
    string Step,
    string? ErrorMessage = null,
    int ChunkCount = 0,
    int PageCount = 0);

/// <summary>
/// Pushes document progress to clients. BLL registers a no-op version; Web replaces it with SignalR.
/// A process that runs only the worker (ROLE=worker) keeps the no-op, and the page falls back to polling.
/// </summary>
public interface IDocumentProgressNotifier
{
    Task NotifyAsync(DocumentProgress progress, CancellationToken ct = default);

    Task NotifyDeletedAsync(string documentId, CancellationToken ct = default);
}

/// <summary>Ingestion settings, section <c>Ingestion</c> in appsettings.</summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public int MaxFileSizeMb { get; set; } = 50;

    /// <summary>Target chunk length in characters.</summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>Characters shared by two neighbouring chunks (clamped to half of <see cref="ChunkSize"/>).</summary>
    public int ChunkOverlap { get; set; } = 150;

    /// <summary>Attempts per docs-stream message before it goes to the dead-letter stream.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>First retry delay; doubles on every retry.</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>Wait between reads when the stream is empty.</summary>
    public int PollIntervalMs { get; set; } = 1000;

    public static readonly string[] AllowedExtensions = [".pdf", ".docx", ".pptx"];
}
