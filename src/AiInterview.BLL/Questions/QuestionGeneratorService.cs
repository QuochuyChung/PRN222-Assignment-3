using System.Text;
using System.Text.Json;
using AiInterview.Common.Enums;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.BLL.Retrieval;

namespace AiInterview.BLL.Questions;

internal sealed class QuestionGeneratorService(IRetrievalService retrieval, ILlmRepository llm) : IQuestionGeneratorService
{
    private const string Instruction = "QUESTION_GENERATION. Bạn là giảng viên. Chỉ dùng các đoạn tài liệu được cung cấp. Trả về duy nhất mảng JSON gồm content, referenceAnswer, keyPoints (mảng chuỗi), bloomLevel (Remember/Understand/Apply/Analyze), sourceChunkIds (mảng ID đúng từ nguồn). Không thêm markdown.";

    public async Task<IReadOnlyList<QuestionDraft>> GenerateAsync(GenerateQuestionsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DocumentIds is null || request.DocumentIds.Count == 0 || request.DocumentIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one document is required.", nameof(request));
        if (request.Count is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(request), "Count must be between 1 and 20.");
        if (request.Levels is { Count: > 0 } && request.Levels.Any(l => !Enum.IsDefined(l)))
            throw new ArgumentException("Invalid Bloom level.", nameof(request));

        var chunks = await retrieval.SearchAsync("Các khái niệm, định nghĩa và ứng dụng quan trọng trong tài liệu", new RetrievalScope(request.DocumentIds), Math.Min(30, Math.Max(5, request.Count * 3)), ct);
        if (chunks.Count == 0) throw new InvalidOperationException("No indexed chunks were found in the selected documents.");
        var ids = chunks.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var prompt = new StringBuilder();
        prompt.AppendLine($"Sinh {request.Count} câu hỏi vấn đáp bằng tiếng Việt. Mức Bloom: {string.Join(", ", request.Levels is { Count: > 0 } ? request.Levels : Enum.GetValues<BloomLevel>())}.");
        prompt.AppendLine($"CHUNK_IDS_JSON:{JsonSerializer.Serialize(chunks.Select(c => c.Id))}");
        foreach (var c in chunks) prompt.AppendLine($"[{c.Id}] Trang {c.Page}: {c.Text}");
        var raw = await llm.GenerateJsonAsync(Instruction, prompt.ToString(), ct);
        return Parse(raw, ids, request.Count, request.Levels);
    }

    internal static IReadOnlyList<QuestionDraft> Parse(string raw, IReadOnlySet<string> allowedIds, int count, IReadOnlyList<BloomLevel>? levels)
    {
        try
        {
            using var json = JsonDocument.Parse(raw);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Question response must be a JSON array.");
            var drafts = new List<QuestionDraft>();
            var seenContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in json.RootElement.EnumerateArray())
            {
                var content = item.GetProperty("content").GetString();
                var answer = item.GetProperty("referenceAnswer").GetString();
                var points = item.GetProperty("keyPoints").EnumerateArray().Select(p => p.GetString() ?? "").ToList();
                var levelText = item.GetProperty("bloomLevel").GetString();
                var sourceIds = item.GetProperty("sourceChunkIds").EnumerateArray().Select(s => s.GetString() ?? "").ToList();
                if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(answer) || points.Count == 0 ||
                    points.Any(string.IsNullOrWhiteSpace) || !Enum.TryParse<BloomLevel>(levelText, true, out var level) ||
                    !Enum.IsDefined(level) || (levels is { Count: > 0 } && !levels.Contains(level)) ||
                    sourceIds.Count == 0 || sourceIds.Any(id => !allowedIds.Contains(id)))
                    throw new FormatException("Question response contains invalid content, Bloom level or source chunk IDs.");
                if (!seenContent.Add(content.Trim()))
                    throw new FormatException("Question response contains duplicate questions.");
                drafts.Add(new QuestionDraft(content.Trim(), answer.Trim(), points, level, sourceIds));
            }
            if (drafts.Count != count) throw new FormatException($"Expected {count} questions, received {drafts.Count}.");
            return drafts;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new FormatException("Question response is not valid JSON in the required format.", ex);
        }
    }
}
