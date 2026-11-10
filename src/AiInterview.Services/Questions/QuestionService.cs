using AiInterview.Common.Enums;
using AiInterview.Data.Entities;
using AiInterview.Data.Repositories.Questions;
using AiInterview.Services.Mapping;

namespace AiInterview.Services.Questions;

internal sealed class QuestionService(IQuestionRepository questions) : IQuestionService
{
    public async Task<IReadOnlyList<QuestionDto>> ListAsync(QuestionStatus? status = null, CancellationToken ct = default)
        => (await questions.ListAsync(status, ct)).Select(q => q.ToDto()).ToList();

    public async Task<QuestionDto?> GetAsync(Guid id, CancellationToken ct = default)
        => (await questions.GetAsync(id, ct))?.ToDto();

    public async Task<IReadOnlyList<QuestionDto>> SaveDraftsAsync(IEnumerable<QuestionDraft> drafts, string? sourceDocumentId, CancellationToken ct = default)
    {
        var entities = drafts.Select(d =>
        {
            Validate(d.Content, d.ReferenceAnswer, d.KeyPoints, d.BloomLevel);
            return new Question
            {
                Content = d.Content.Trim(), ReferenceAnswer = d.ReferenceAnswer.Trim(),
                KeyPoints = d.KeyPoints.Select(x => x.Trim()).ToList(), BloomLevel = d.BloomLevel,
                SourceDocumentId = sourceDocumentId, SourceChunkIds = d.SourceChunkIds.ToList(), Status = QuestionStatus.Draft
            };
        }).ToList();
        if (entities.Count > 0) await questions.AddRangeAsync(entities, ct);
        return entities.Select(q => q.ToDto()).ToList();
    }

    public async Task UpdateAsync(UpdateQuestionRequest request, CancellationToken ct = default)
    {
        var question = await RequiredAsync(request.Id, ct);
        if (question.Status != QuestionStatus.Draft) throw new InvalidOperationException("Only draft questions can be edited.");
        Validate(request.Content, request.ReferenceAnswer, request.KeyPoints, request.BloomLevel);
        question.Content = request.Content.Trim();
        question.ReferenceAnswer = request.ReferenceAnswer.Trim();
        question.KeyPoints = request.KeyPoints.Select(x => x.Trim()).ToList();
        question.BloomLevel = request.BloomLevel;
        await questions.UpdateAsync(question, ct);
    }

    public async Task ApproveAsync(Guid id, CancellationToken ct = default) => await SetStatusAsync(id, QuestionStatus.Approved, ct);
    public async Task RejectAsync(Guid id, CancellationToken ct = default) => await SetStatusAsync(id, QuestionStatus.Rejected, ct);
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await RequiredAsync(id, ct);
        await questions.DeleteAsync(id, ct);
    }

    private async Task SetStatusAsync(Guid id, QuestionStatus status, CancellationToken ct)
    {
        var question = await RequiredAsync(id, ct);
        if (question.Status != QuestionStatus.Draft) throw new InvalidOperationException("Only draft questions can be reviewed.");
        question.Status = status;
        await questions.UpdateAsync(question, ct);
    }

    private async Task<Question> RequiredAsync(Guid id, CancellationToken ct)
        => await questions.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Question {id} was not found.");

    private static void Validate(string content, string answer, IReadOnlyList<string> keyPoints, BloomLevel level)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(answer) || keyPoints is null ||
            keyPoints.Count == 0 || keyPoints.Any(string.IsNullOrWhiteSpace) || !Enum.IsDefined(level))
            throw new ArgumentException("Question, reference answer, key points and Bloom level are required.");
    }
}
