using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Interview;

// Dev D — UI-03..UI-07 (giao diện phòng phỏng vấn, TTS, STT, client state machine).
// Dev C — ENG-07: handler/API start, state, submit-answer, skip, finish (gọi IInterviewSessionService).
public class RoomModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev D (UI) + Dev C (endpoints)",
        "UI-03..UI-07, ENG-07",
        "The AI reads the question aloud, the student answers by voice, and the AI asks follow-ups within the time and turn limits.");

    public void OnGet()
    {
    }
}
