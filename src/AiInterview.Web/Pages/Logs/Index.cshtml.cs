using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Logs;

// Dev B — LOG-02 (xem log một phiên thi). Inject ITurnLogService vào constructor.
public class IndexModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev B",
        "LOG-02",
        "Xem câu hỏi, transcript, lượt hỏi xoáy, lý do và nguồn trích dẫn của từng phiên thi.");

    public void OnGet()
    {
    }
}
