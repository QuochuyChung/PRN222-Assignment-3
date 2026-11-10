using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AiInterview.Web.Pages.Documents;

public class UploadModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly ILogger<UploadModel> _logger;

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    public string? ErrorMessage { get; set; }

    public UploadModel(
        IDocumentService docService,
        ILogger<UploadModel> logger)
    {
        _docService = docService;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (UploadFile == null || UploadFile.Length == 0)
        {
            ErrorMessage = "Vui lòng chọn một file tài liệu để tải lên.";
            return Page();
        }

        string fileName = Path.GetFileName(UploadFile.FileName);
        string ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext != ".pdf" && ext != ".docx" && ext != ".pptx")
        {
            ErrorMessage = $"Định dạng '{ext}' không được hỗ trợ. Chỉ chấp nhận các file: .pdf, .docx, .pptx.";
            return Page();
        }

        const long maxBytes = 50 * 1024 * 1024; // 50MB
        if (UploadFile.Length > maxBytes)
        {
            ErrorMessage = "Dung lượng file vượt quá giới hạn 50MB.";
            return Page();
        }

        try
        {
            using var stream = UploadFile.OpenReadStream();
            var metadata = await _docService.UploadDocumentAsync(
                stream,
                fileName,
                UploadFile.ContentType ?? "application/octet-stream",
                UploadFile.Length,
                ct);

            _logger.LogInformation("UploadModel.cs - App: Người dùng upload file thành công, documentId={DocId}, fileName={FileName}",
                metadata.Id, fileName);

            TempData["StatusMessage"] = $"Tải lên tài liệu '{fileName}' thành công. Hệ thống đang tiến hành trích xuất văn bản và tạo embedding.";
            return RedirectToPage("/Documents/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadModel.cs - App: Lỗi khi xử lý file upload, fileName={FileName}", fileName);
            ErrorMessage = $"Tải lên thất bại: {ex.Message}";
            return Page();
        }
    }
}

