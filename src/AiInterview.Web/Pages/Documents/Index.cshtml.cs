using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Documents;

// Dev A — ING-02 (upload), ING-08 (danh sách, tiến độ SignalR, xóa, re-index).
// Inject IDocumentService vào constructor; PageModel không được gọi repository trực tiếp.
public class IndexModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev A",
        "ING-02, ING-08",
        "Upload tài liệu (PPTX/PDF/DOCX), xem trạng thái xử lý, xóa và re-index.");

    public void OnGet()
    {
    }
}
