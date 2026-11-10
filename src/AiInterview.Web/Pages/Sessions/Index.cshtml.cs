using AiInterview.Common.Enums;
using AiInterview.Services.Interview;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Sessions;

/// <summary>
/// Quản lý danh sách phiên thi và tạo mới phiên thi (Dev D - UI-02).
/// Tuân thủ kiến trúc 3 lớp: Chỉ inject và gọi Service, không tham chiếu Data.
/// </summary>
public class IndexModel(IInterviewSessionService sessionService) : PageModel
{
    public IReadOnlyList<SessionDto> Sessions { get; private set; } = Array.Empty<SessionDto>();

    [BindProperty]
    public CreateSessionForm Form { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Sessions = await sessionService.ListAsync(ct: ct);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Sessions = await sessionService.ListAsync(ct: ct);
            return Page();
        }

        var request = new CreateSessionRequest(
            Title: string.IsNullOrWhiteSpace(Form.Title) ? "Phỏng vấn vấn đáp AI" : Form.Title.Trim(),
            StudentName: string.IsNullOrWhiteSpace(Form.StudentName) ? "Thí sinh" : Form.StudentName.Trim(),
            QuestionCount: Form.QuestionCount,
            MaxFollowUps: Form.MaxFollowUps,
            AnswerSeconds: Form.AnswerSeconds);

        var created = await sessionService.CreateAsync(request, ct);
        return RedirectToPage("/Interview/Room", new { sessionId = created.Id });
    }

    public class CreateSessionForm
    {
        public string Title { get; set; } = "Phỏng vấn kiến thức C# & Web";
        public string StudentName { get; set; } = "Nguyễn Văn A";
        public int QuestionCount { get; set; } = 3;
        public int MaxFollowUps { get; set; } = 2;
        public int AnswerSeconds { get; set; } = 60;
    }
}
