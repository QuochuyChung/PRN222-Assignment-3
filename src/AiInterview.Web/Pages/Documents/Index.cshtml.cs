using AiInterview.Common.Enums;
using AiInterview.Services.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AiInterview.Web.Pages.Documents;

/// <summary>Dev A, ING-02 (upload) and ING-08 (list, live progress, delete, re-index). Talks to <see cref="IDocumentService"/> only.</summary>
// ponytail: fixed request cap a little above the 50 MB default of Ingestion:MaxFileSizeMb; raise both together.
[RequestSizeLimit(UploadLimitBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = UploadLimitBytes)]
public class IndexModel(IDocumentService documents, IOptions<IngestionOptions> options, ILogger<IndexModel> logger) : PageModel
{
    private const long UploadLimitBytes = 55L * 1024 * 1024;

    public IReadOnlyList<DocumentDto> Documents { get; private set; } = [];

    public int MaxFileSizeMb => options.Value.MaxFileSizeMb;

    [TempData] public string? Notice { get; set; }

    [TempData] public string? Problem { get; set; }

    public int CountOf(DocumentStatus status) => Documents.Count(d => d.Status == status);

    public async Task OnGetAsync(CancellationToken ct) => Documents = await documents.ListAsync(null, ct);

    /// <summary>Current status as JSON, used by the polling fallback: <c>?handler=Status</c> or <c>?handler=Status&amp;id=...</c>.</summary>
    public async Task<IActionResult> OnGetStatusAsync(string? id, CancellationToken ct)
    {
        if (id is null) return new JsonResult((await documents.ListAsync(null, ct)).Select(ToStatus));
        var document = await documents.GetAsync(id, ct);
        return document is null ? NotFound() : new JsonResult(ToStatus(document));
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Fail("Choose a .pdf, .docx or .pptx file first.");

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await documents.UploadAsync(new UploadDocumentRequest(file.FileName, file.ContentType, stream, file.Length, "Lecturer"), ct);
            Notice = $"\"{document.FileName}\" was uploaded. Processing runs in the background.";
        }
        catch (ArgumentException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Index.cshtml.cs - App: Upload failed, fileName={FileName}", file.FileName);
            return Fail("The upload failed because of a server error. Please try again.");
        }

        return RedirectToPage();
    }

    public Task<IActionResult> OnPostDeleteAsync(string id, CancellationToken ct)
        => Run(() => documents.DeleteAsync(id, ct), "Document deleted, including its vectors and the original file.", "delete", id);

    public Task<IActionResult> OnPostReindexAsync(string id, CancellationToken ct)
        => Run(() => documents.ReindexAsync(id, ct), "Re-index queued.", "re-index", id);

    private async Task<IActionResult> Run(Func<Task> action, string success, string verb, string id)
    {
        try
        {
            await action();
            Notice = success;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            Problem = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Index.cshtml.cs - App: Could not {Verb} document, documentId={DocId}", verb, id);
            Problem = $"Could not {verb} the document. Please try again.";
        }

        return RedirectToPage();
    }

    private IActionResult Fail(string message)
    {
        Problem = message;
        return RedirectToPage();
    }

    private static object ToStatus(DocumentDto d) => new
    {
        id = d.Id,
        status = d.Status.ToString(),
        errorMessage = d.ErrorMessage,
        chunkCount = d.ChunkCount,
        pageCount = d.PageCount,
        updatedAt = d.UpdatedAt
    };
}
