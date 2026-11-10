using AiInterview.Data.Entities;
using AiInterview.Data.Models;

namespace AiInterview.Services.Documents;

public sealed record UploadDocumentRequest(string FileName, string ContentType, Stream Content, long Size, string? UploadedBy);

public sealed record ExtractedPage(int Index, string Text);

public sealed record ExtractedDocument(string FileName, IReadOnlyList<ExtractedPage> Pages);

public sealed record ChunkDraft(int ChunkIndex, int Page, string Text);

/// <summary>
/// Quản lý tài liệu cho trang upload/danh sách. Dev A (ING-02, ING-08).
/// Upload: lưu file (IFileRepository) -> ghi metadata Processing (IDocumentRepository) -> đẩy docs-stream. Đúng thứ tự này.
/// </summary>
public interface IDocumentService
{
    Task<DocumentRecord> UploadAsync(UploadDocumentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRecord>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default);

    Task<DocumentRecord?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Xóa ở cả metadata, vector và file gốc trong một hàm duy nhất.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Chạy lại pipeline (ví dụ khi đổi model embedding).</summary>
    Task ReindexAsync(string id, CancellationToken ct = default);
}

/// <summary>Trích văn bản từ PPTX/PDF/DOCX. Dev A (ING-03). PDF scan không có text phải báo lỗi rõ ràng.</summary>
public interface IDocumentTextExtractor
{
    Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}

/// <summary>Chia văn bản thành chunk, giữ số trang/slide. Dev A (ING-04).</summary>
public interface IChunker
{
    IReadOnlyList<ChunkDraft> Chunk(ExtractedDocument document);
}

/// <summary>Pipeline xử lý một tài liệu: trích text, chunk, embedding, ghi vector, cập nhật trạng thái. Dev A (ING-07).</summary>
public interface IDocumentProcessingService
{
    Task ProcessAsync(string documentId, CancellationToken ct = default);
}
