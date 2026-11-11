using AiInterview.Common.Enums;
using AiInterview.BLL.Documents;
using AiInterview.BLL.Questions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AiInterview.Web.Pages.Questions;

public class IndexModel(IQuestionGeneratorService generator, IQuestionService questions, IDocumentService documents, ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<QuestionDto> Questions { get; private set; } = [];
    public IReadOnlyList<DocumentDto> Documents { get; private set; } = [];
    [TempData] public string? Notice { get; set; }
    [TempData] public string? Problem { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Questions = await questions.ListAsync(null, ct);
        Documents = await documents.ListAsync(DocumentStatus.Success, ct);
    }

    public async Task<IActionResult> OnPostGenerateAsync(string[] documentIds, int count, BloomLevel[] levels, CancellationToken ct)
    {
        try
        {
            var allowed = (await documents.ListAsync(DocumentStatus.Success, ct)).Select(d => d.Id).ToHashSet();
            if (documentIds.Length == 0 || documentIds.Any(id => !allowed.Contains(id))) throw new ArgumentException("Choose indexed documents.");
            var drafts = await generator.GenerateAsync(new GenerateQuestionsRequest(documentIds, count, levels), ct);
            await questions.SaveDraftsAsync(drafts, documentIds.Length == 1 ? documentIds[0] : null, ct);
            Notice = $"Created {drafts.Count} draft questions.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException) { Problem = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Index.cshtml.cs - App: Question generation failed, documentIds={DocumentIds}", string.Join(',', documentIds));
            Problem = "Question generation failed. Please try again.";
        }
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct) => Run(() => questions.ApproveAsync(id, ct), id, "approved");
    public Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct) => Run(() => questions.RejectAsync(id, ct), id, "rejected");
    public Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct) => Run(() => questions.DeleteAsync(id, ct), id, "deleted");
    public Task<IActionResult> OnPostUpdateAsync(Guid id, string content, string referenceAnswer, string keyPoints, BloomLevel bloomLevel, CancellationToken ct)
        => Run(() => questions.UpdateAsync(new UpdateQuestionRequest(id, content, referenceAnswer,
            keyPoints.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), bloomLevel), ct), id, "updated");

    private async Task<IActionResult> Run(Func<Task> action, Guid id, string verb)
    {
        try { await action(); Notice = $"Question {verb}."; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { Problem = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Index.cshtml.cs - App: Question action failed, questionId={QuestionId}, action={Action}", id, verb);
            Problem = "Question action failed. Please try again.";
        }
        return RedirectToPage();
    }
}
