using AiInterview.Common.Enums;

namespace AiInterview.Services.Documents;

/// <summary>Tài liệu trả cho giao diện. Không chứa chi tiết nội bộ như key lưu trữ.</summary>
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
/// Quản lý tài liệu cho trang upload/danh sách. Dev A (ING-02, ING-08).
/// Upload: lưu file (IFileRepository) -> ghi metadata Processing (IDocumentRepository) -> đẩy docs-stream. Đúng thứ tự này.
/// Trả <see cref="DocumentDto"/>, không trả model của lớp Data.
/// </summary>
public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentStatus? status = null, CancellationToken ct = default);

    Task<DocumentDto?> GetAsync(string id, CancellationToken ct = default);

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
