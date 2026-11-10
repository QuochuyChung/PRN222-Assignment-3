using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Exceptions;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly IFileStorage _fileStorage;
    private readonly IDocumentRepository _docRepo;
    private readonly IDocumentStreamPublisher _streamPublisher;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IDocumentProgressNotifier? _notifier;
    private readonly ILogger<DocumentService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pptx", ".pdf", ".docx"
    };

    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

    public DocumentService(
        IFileStorage fileStorage,
        IDocumentRepository docRepo,
        IDocumentStreamPublisher streamPublisher,
        IDocumentTextExtractor extractor,
        IChunker chunker,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<DocumentService> logger,
        IDocumentProgressNotifier? notifier = null)
    {
        _fileStorage = fileStorage;
        _docRepo = docRepo;
        _streamPublisher = streamPublisher;
        _extractor = extractor;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
        _notifier = notifier;
    }

    public async Task<DocumentMetadata> UploadDocumentAsync(
        Stream content,
        string fileName,
        string contentType,
        long size,
        CancellationToken ct = default)
    {
        string ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException($"Định dạng file không hợp lệ '{ext}'. Chỉ chấp nhận .pptx, .pdf, .docx.");
        }

        if (size > MaxFileSize)
        {
            throw new ArgumentException($"Dung lượng file vượt quá giới hạn cho phép (tối đa {MaxFileSize / (1024 * 1024)}MB).");
        }

        string documentId = Guid.NewGuid().ToString("N");
        string storageKey = $"{documentId}/{fileName}";

        // 1. Thứ tự: file → Mongo → stream
        await _fileStorage.SaveAsync(storageKey, content, contentType, ct);

        var metadata = new DocumentMetadata
        {
            Id = documentId,
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            StorageKey = storageKey,
            Status = DocumentStatus.Processing,
            UploadedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _docRepo.CreateAsync(metadata, ct);

        await _streamPublisher.PublishAsync(new DocStreamMessage
        {
            DocumentId = documentId,
            StorageKey = storageKey
        }, ct);

        if (_notifier != null)
        {
            await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Processing.ToString(), null, 0, 0);
        }

        _logger.LogInformation("DocumentService.cs - App: Upload và enqueue tài liệu thành công, documentId={DocId}, fileName={FileName}",
            documentId, fileName);

        return metadata;
    }

    public async Task ProcessDocumentAsync(string documentId, string storageKey, CancellationToken ct = default)
    {
        var doc = await _docRepo.GetByIdAsync(documentId, ct);
        if (doc == null)
        {
            _logger.LogError("DocumentService.cs - App: Không tìm thấy tài liệu để xử lý, documentId={DocId}", documentId);
            return;
        }

        try
        {
            if (_notifier != null) await _notifier.NotifyProgressAsync(documentId, "Đang tải file từ kho lưu trữ", 15);

            using var stream = await _fileStorage.OpenReadAsync(storageKey, ct);

            if (_notifier != null) await _notifier.NotifyProgressAsync(documentId, "Đang trích xuất văn bản", 35);
            var extracted = await _extractor.ExtractAsync(stream, doc.FileName, ct);
            extracted.DocumentId = documentId;

            if (_notifier != null) await _notifier.NotifyProgressAsync(documentId, "Đang chia đoạn văn bản (chunking)", 55);
            var chunks = _chunker.Chunk(extracted);

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("Không thể tạo các đoạn văn bản từ tài liệu này.");
            }

            if (_notifier != null) await _notifier.NotifyProgressAsync(documentId, "Đang tạo vector embedding", 75);
            var chunkTexts = chunks.Select(c => c.Text).ToList();
            var embeddings = await _embeddingService.EmbedAsync(chunkTexts, ct);

            var vectorPoints = new List<VectorPoint>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                vectorPoints.Add(new VectorPoint
                {
                    Id = $"{documentId}:{chunk.ChunkIndex}",
                    DocumentId = documentId,
                    ChunkIndex = chunk.ChunkIndex,
                    Page = chunk.Page,
                    Text = chunk.Text,
                    Vector = embeddings[i]
                });
            }

            if (_notifier != null) await _notifier.NotifyProgressAsync(documentId, "Đang lưu vector vào Qdrant", 90);
            await _vectorStore.UpsertAsync(vectorPoints, ct);

            await _docRepo.UpdateStatusAsync(
                documentId,
                DocumentStatus.Success,
                errorMessage: null,
                chunkCount: chunks.Count,
                pageCount: extracted.PageCount,
                ct: ct);

            if (_notifier != null)
            {
                await _notifier.NotifyProgressAsync(documentId, "Hoàn tất xử lý tài liệu", 100);
                await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Success.ToString(), null, chunks.Count, extracted.PageCount);
            }

            _logger.LogInformation("DocumentService.cs - App: Xử lý tài liệu hoàn tất thành công, documentId={DocId}, số chunk={ChunkCount}, số trang={PageCount}",
                documentId, chunks.Count, extracted.PageCount);
        }
        catch (ScannedPdfException ex)
        {
            _logger.LogWarning(ex, "DocumentService.cs - App: Thất bại do PDF scan/không có text, documentId={DocId}", documentId);
            await _docRepo.UpdateStatusAsync(documentId, DocumentStatus.Failed, ex.Message, ct: ct);
            if (_notifier != null)
            {
                await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Failed.ToString(), ex.Message, 0, 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentService.cs - App: Lỗi khi xử lý tài liệu, documentId={DocId}", documentId);
            await _docRepo.UpdateStatusAsync(documentId, DocumentStatus.Failed, ex.Message, ct: ct);
            if (_notifier != null)
            {
                await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Failed.ToString(), ex.Message, 0, 0);
            }
            throw;
        }
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        var doc = await _docRepo.GetByIdAsync(documentId, ct);
        if (doc != null && !string.IsNullOrEmpty(doc.StorageKey))
        {
            try
            {
                await _fileStorage.DeleteAsync(doc.StorageKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DocumentService.cs - App: Không thể xóa file MinIO/Storage, documentId={DocId}", documentId);
            }
        }

        try
        {
            await _vectorStore.DeleteByDocumentAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DocumentService.cs - App: Không thể xóa vector trong Qdrant, documentId={DocId}", documentId);
        }

        await _docRepo.DeleteAsync(documentId, ct);

        if (_notifier != null)
        {
            await _notifier.NotifyStatusChangedAsync(documentId, "Deleted", null, 0, 0);
        }

        _logger.LogInformation("DocumentService.cs - App: Xóa toàn bộ tài liệu (Storage + Vector + Repo) thành công, documentId={DocId}",
            documentId);
    }

    public async Task ReindexDocumentAsync(string documentId, CancellationToken ct = default)
    {
        var doc = await _docRepo.GetByIdAsync(documentId, ct);
        if (doc == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy tài liệu với ID: {documentId}");
        }

        await _vectorStore.DeleteByDocumentAsync(documentId, ct);
        await _docRepo.UpdateStatusAsync(documentId, DocumentStatus.Processing, null, ct: ct);

        await _streamPublisher.PublishAsync(new DocStreamMessage
        {
            DocumentId = documentId,
            StorageKey = doc.StorageKey
        }, ct);

        if (_notifier != null)
        {
            await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Processing.ToString(), null, 0, 0);
        }

        _logger.LogInformation("DocumentService.cs - App: Re-index tài liệu được kích hoạt, documentId={DocId}", documentId);
    }
}

