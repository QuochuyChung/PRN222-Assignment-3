using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AiInterview.Web.Pages.Documents;

public class IndexModel : PageModel
{
    private readonly IDocumentRepository _repo;
    private readonly IDocumentService _docService;
    private readonly ILogger<IndexModel> _logger;

    public IReadOnlyList<DocumentMetadata> Documents { get; set; } = Array.Empty<DocumentMetadata>();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IndexModel(
        IDocumentRepository repo,
        IDocumentService docService,
        ILogger<IndexModel> logger)
    {
        _repo = repo;
        _docService = docService;
        _logger = logger;
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Documents = await _repo.ListAsync(ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string documentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(documentId))
        {
            ErrorMessage = "ID tài liệu không hợp lệ.";
            return RedirectToPage();
        }

        try
        {
            await _docService.DeleteDocumentAsync(documentId, ct);
            StatusMessage = "Xóa tài liệu và dữ liệu vector liên quan thành công.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IndexModel.cs - App: Lỗi khi xóa tài liệu, documentId={DocId}", documentId);
            ErrorMessage = $"Không thể xóa tài liệu: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReindexAsync(string documentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(documentId))
        {
            ErrorMessage = "ID tài liệu không hợp lệ.";
            return RedirectToPage();
        }

        try
        {
            await _docService.ReindexDocumentAsync(documentId, ct);
            StatusMessage = "Đã đưa tài liệu vào hàng đợi xử lý lại (Re-index).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IndexModel.cs - App: Lỗi khi re-index tài liệu, documentId={DocId}", documentId);
            ErrorMessage = $"Không thể re-index: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetStatusAsync(string documentId, CancellationToken ct)
    {
        var doc = await _repo.GetByIdAsync(documentId, ct);
        if (doc == null)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            documentId = doc.Id,
            status = doc.Status.ToString(),
            errorMessage = doc.ErrorMessage,
            chunkCount = doc.ChunkCount,
            pageCount = doc.PageCount,
            updatedAt = doc.UpdatedAt
        });
    }
}

