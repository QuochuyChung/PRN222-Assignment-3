using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Interview;

// Dev D — UI-03..UI-07 (giao diện phòng phỏng vấn, TTS, STT, client state machine).
// Dev C — ENG-07: handler/API start, state, submit-answer, skip, finish (gọi IInterviewSessionService).
public class RoomModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev D (giao diện) + Dev C (endpoint)",
        "UI-03..UI-07, ENG-07",
        "AI đọc câu hỏi, sinh viên trả lời bằng giọng nói, AI hỏi xoáy theo giới hạn thời gian và số lượt.");

    public void OnGet()
    {
    }
}
