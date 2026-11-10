using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Sessions;

// Dev D — UI-02 (chọn/bắt đầu phiên thi, test mic). Dev C cung cấp IInterviewSessionService (ENG-02).
public class IndexModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev D (giao diện) + Dev C (service)",
        "UI-02, ENG-02",
        "Tạo phiên thi, chọn số câu / số lượt hỏi xoáy / thời gian trả lời, kiểm tra mic trước khi bắt đầu.");

    public void OnGet()
    {
    }
}
