using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Sessions;

// Dev D — UI-02 (chọn/bắt đầu phiên thi, test mic). Dev C cung cấp IInterviewSessionService (ENG-02).
public class IndexModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev D (UI) + Dev C (service)",
        "UI-02, ENG-02",
        "Create a session, pick the number of questions, follow-ups and answer time, and test the microphone before starting.");

    public void OnGet()
    {
    }
}
