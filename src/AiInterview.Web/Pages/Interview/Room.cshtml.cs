using AiInterview.Common.Enums;
using AiInterview.Services.Interview;
using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Interview;

// Dev D — UI-03..UI-07 (giao diện phòng phỏng vấn, TTS, STT, client state machine).
// Dev C — ENG-07: handler start, state, submit-answer, skip, finish (gọi IInterviewSessionService).
//
// Hợp đồng cho client (Dev D):
//   GET  /Interview/Room?sessionId=..        → render trang, nạp sẵn State (sau F5)
//   GET  /Interview/Room?handler=State&sessionId=..
//   POST /Interview/Room?handler=Start&sessionId=..
//   POST /Interview/Room?handler=SubmitAnswer&sessionId=..&turnId=..&transcript=..
//   POST /Interview/Room?handler=Skip&sessionId=..
//   POST /Interview/Room?handler=Finish&sessionId=..
// POST cần token chống CSRF: đặt @Html.AntiForgeryToken() trong .cshtml rồi gửi header
// "RequestVerificationToken". Lỗi trả về JSON { error } với mã 400/404/409.
public class RoomModel(IInterviewSessionService interviews) : PageModel
{
    /// <summary>Thông tin trang khung — Dev D bỏ khi thay phần thân trang bằng UI thật.</summary>
    public PlaceholderInfo Info { get; } = new(
        "Dev D (UI) + Dev C (endpoints)",
        "UI-03..UI-07, ENG-07",
        "The AI reads the question aloud, the student answers by voice, and the AI asks follow-ups within the time and turn limits.");

    /// <summary>Trạng thái hiện tại, nạp sẵn khi tải trang để F5 không mất phiên (ENG-09).</summary>
    public InterviewStateView? State { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? sessionId, CancellationToken ct)
    {
        if (sessionId is null)
        {
            return Page();
        }

        State = await interviews.GetStateAsync(sessionId.Value, ct);
        return Page();
    }

    /// <summary>Client polling sau khi F5 hoặc khi mất SignalR. Trả <c>remainingSeconds</c> từ server.</summary>
    public async Task<IActionResult> OnGetStateAsync(Guid sessionId, CancellationToken ct)
    {
        var state = await interviews.GetStateAsync(sessionId, ct);
        return state is null ? NotFound(new { error = "This interview is not running." }) : new JsonResult(state);
    }

    public async Task<IActionResult> OnPostStartAsync(Guid sessionId, CancellationToken ct)
        => await RunAsync(async () => new JsonResult(await interviews.StartAsync(sessionId, ct)));

    public async Task<IActionResult> OnPostSubmitAnswerAsync(
        Guid sessionId, string? turnId, string? transcript, CancellationToken ct)
    {
        // Client sinh turnId; thiếu thì server sinh hộ để lượt vẫn chống trùng được khi client retry.
        turnId = string.IsNullOrWhiteSpace(turnId) ? Guid.NewGuid().ToString("N") : turnId.Trim();

        return await RunAsync(async () =>
            new JsonResult(await interviews.SubmitAnswerAsync(
                new SubmitAnswerRequest(sessionId, turnId, transcript ?? ""), ct)));
    }

    public async Task<IActionResult> OnPostSkipAsync(Guid sessionId, CancellationToken ct)
        => await RunAsync(async () => new JsonResult(await interviews.SkipAsync(sessionId, ct)));

    public async Task<IActionResult> OnPostFinishAsync(Guid sessionId, CancellationToken ct)
    {
        await interviews.FinishAsync(sessionId, ct);
        return new JsonResult(new { finished = true });
    }

    /// <summary>Dịch lỗi nghiệp vụ thành mã HTTP để client (UI-07) hiển thị đúng thông báo.</summary>
    private async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Phiên đã kết thúc / chưa chạy / ngân hàng câu hỏi không đủ.
            return new ConflictObjectResult(new { error = ex.Message });
        }
    }
}
