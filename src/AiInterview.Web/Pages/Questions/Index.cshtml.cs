using AiInterview.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Questions;

// Dev B — QST-04 (sinh câu hỏi), QST-05 (duyệt/sửa/loại bỏ).
// Inject IQuestionGeneratorService và IQuestionService vào constructor.
public class IndexModel : PageModel
{
    public PlaceholderInfo Info { get; } = new(
        "Dev B",
        "QST-04, QST-05",
        "Sinh câu hỏi từ tài liệu bằng AI, giảng viên duyệt/sửa/loại bỏ trước khi vào ngân hàng chính thức.");

    public void OnGet()
    {
    }
}
