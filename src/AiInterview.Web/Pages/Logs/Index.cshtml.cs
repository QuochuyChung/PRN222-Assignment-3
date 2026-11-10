using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Logs;

// Dev B — LOG-02 (xem log một phiên thi). Inject ITurnLogService vào constructor.
public class IndexModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev B",
        "LOG-02",
        "See the questions, transcripts, follow-ups, reasons and cited sources of each session.");

    public void OnGet()
    {
    }
}
