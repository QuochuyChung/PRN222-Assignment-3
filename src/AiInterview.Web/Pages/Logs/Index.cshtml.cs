using AiInterview.Services.TurnLog;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Logs;

public class IndexModel(ITurnLogService turns) : PageModel
{
    public IReadOnlyList<TurnDto> Turns { get; private set; } = [];
    public Guid? SessionId { get; private set; }

    public async Task OnGetAsync(Guid? sessionId, CancellationToken ct)
    {
        SessionId = sessionId;
        Turns = sessionId is { } id ? await turns.ListBySessionAsync(id, ct) : await turns.ListRecentAsync(30, ct);
    }
}
