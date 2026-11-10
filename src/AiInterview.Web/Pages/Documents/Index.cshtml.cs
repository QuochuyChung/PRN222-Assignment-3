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
            ErrorMessage = "Invalid document ID.";
            return RedirectToPage();
        }

        try
        {
            await _docService.DeleteDocumentAsync(documentId, ct);
            StatusMessage = "Document and its vectors were deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IndexModel.cs - App: Failed to delete document, documentId={DocId}", documentId);
            ErrorMessage = $"Could not delete the document: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReindexAsync(string documentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(documentId))
        {
            ErrorMessage = "Invalid document ID.";
            return RedirectToPage();
        }

        try
        {
            await _docService.ReindexDocumentAsync(documentId, ct);
            StatusMessage = "Document queued for re-indexing.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IndexModel.cs - App: Failed to re-index document, documentId={DocId}", documentId);
            ErrorMessage = $"Could not re-index: {ex.Message}";
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

