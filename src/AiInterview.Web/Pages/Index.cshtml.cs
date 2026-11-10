using AiInterview.Services.Dashboard;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages;

/// <summary>
/// Dashboard mẫu cho kiến trúc 3 lớp: PageModel (code-behind) -> IDashboardService -> repository.
/// Dev D (UI-08) hoàn thiện giao diện; luồng dữ liệu giữ nguyên.
/// </summary>
public class IndexModel(IDashboardService dashboard) : PageModel
{
    public DashboardSummary Summary { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Summary = await dashboard.GetSummaryAsync(ct);
    }
}
