using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiInterview.DAL.Repositories.Ai;

/// <summary>
/// LLM giả cho chế độ Fake. Nhận diện loại prompt qua dấu hiệu trong prompt (không phải tên file):
/// <list type="bullet">
/// <item><c>QUESTION_GENERATION</c> — sinh câu hỏi từ các chunk được cung cấp (QST-02).</item>
/// <item>Prompt giám khảo của <c>FollowUpDecider</c> — trả kết luận hỏi xoáy theo kịch bản
/// xem trong <c>seed/engine/answers.md</c> (đủ ý / thiếu ý / mơ hồ) để demo không cần Gemini (ENG-04).</item>
/// </list>
/// </summary>
public sealed class FakeLlmRepository : ILlmRepository
{
    public Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default)
    {
        if (systemInstruction.Contains("QUESTION_GENERATION", StringComparison.Ordinal))
        {
            return Task.FromResult(GenerateQuestions(userPrompt));
        }

        // FollowUpDecider: prompt có mục "Câu trả lời của sinh viên" và "Các ý chính bắt buộc".
        if (userPrompt.Contains("## Câu trả lời của sinh viên", StringComparison.Ordinal))
        {
            return Task.FromResult(DecideFollowUp(userPrompt));
        }

        return Task.FromResult(NoFollowUp("Đã đủ ý chính"));
    }

    private static string GenerateQuestions(string userPrompt)
    {
        var ids = new List<string>();
        var marker = "CHUNK_IDS_JSON:";
        var start = userPrompt.IndexOf(marker, StringComparison.Ordinal);
        if (start >= 0)
        {
            var line = userPrompt[(start + marker.Length)..].Split('\n')[0];
            ids = JsonSerializer.Deserialize<List<string>>(line) ?? [];
        }

        var count = Regex.Match(userPrompt, @"Sinh (\d+) câu");
        var requested = count.Success ? Math.Clamp(int.Parse(count.Groups[1].Value), 1, 20) : 1;
        var level = Regex.Match(userPrompt, @"Mức Bloom: ([A-Za-z]+)");
        return JsonSerializer.Serialize(Enumerable.Range(1, requested).Select(i => new
        {
            content = $"Câu {i}: Nêu ý chính của đoạn tài liệu được cung cấp?",
            referenceAnswer = "Trình bày chính xác nội dung và ví dụ từ đoạn tài liệu.",
            keyPoints = new[] { "Nội dung chính", "Ví dụ" }, bloomLevel = level.Success ? level.Groups[1].Value : "Understand", sourceChunkIds = ids.Take(1).ToArray()
        }));
    }

    /// <summary>
    /// Kịch bản theo mức độ đầy đủ của câu trả lời, khớp với <c>seed/engine/answers.md</c>.
    /// Chỉ là dữ liệu demo cho chế độ Fake; luật cứng (hết giờ / hết lượt xoáy) vẫn do server áp.
    /// </summary>
    private static string DecideFollowUp(string userPrompt)
    {
        var transcript = ReadSection(userPrompt, "## Câu trả lời của sinh viên");
        var normalizedTranscript = Normalize(transcript);
        var keyPoints = ReadKeyPoints(userPrompt);

        // Mơ hồ: không nêu được ý nào.
        if (normalizedTranscript.Length == 0 ||
            normalizedTranscript.Contains("na na", StringComparison.Ordinal) ||
            normalizedTranscript.Contains("khong nho", StringComparison.Ordinal) ||
            normalizedTranscript.Contains("khong biet", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new
            {
                needFollowUp = true,
                followUpQuestion = "Bạn nói rõ hơn: Razor Pages tổ chức trang và routing như thế nào?",
                reason = "Câu trả lời mơ hồ, không nêu được ý chính nào.",
                coveredKeyPoints = Array.Empty<string>(),
                missingKeyPoints = keyPoints.ToArray(),
                sources = Array.Empty<string>()
            });
        }

        var covered = keyPoints.Where(p => Covers(normalizedTranscript, p)).ToArray();
        var missing = keyPoints.Where(p => !covered.Contains(p)).ToArray();

        if (missing.Length == 0)
        {
            return JsonSerializer.Serialize(new
            {
                needFollowUp = false,
                followUpQuestion = "",
                reason = "Đã đủ các ý chính.",
                coveredKeyPoints = covered,
                missingKeyPoints = Array.Empty<string>(),
                sources = Array.Empty<string>()
            });
        }

        return JsonSerializer.Serialize(new
        {
            needFollowUp = true,
            followUpQuestion = "Khi nào bạn chọn Razor Pages thay vì MVC?",
            reason = $"Còn thiếu {missing.Length} ý chính.",
            coveredKeyPoints = covered,
            missingKeyPoints = missing,
            sources = Array.Empty<string>()
        });
    }

    /// <summary>Đọc nội dung một mục trong prompt (tới dòng trống hoặc tiêu đề kế tiếp).</summary>
    private static string ReadSection(string prompt, string header)
    {
        var start = prompt.IndexOf(header, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var bodyStart = prompt.IndexOf('\n', start);
        if (bodyStart < 0)
        {
            return "";
        }

        var rest = prompt[(bodyStart + 1)..];
        var end = rest.IndexOf("\n## ", StringComparison.Ordinal);
        return (end >= 0 ? rest[..end] : rest).Trim();
    }

    /// <summary>Các dòng "- ý chính" nằm trong mục "Các ý chính bắt buộc".</summary>
    private static List<string> ReadKeyPoints(string prompt)
    {
        var section = ReadSection(prompt, "## Các ý chính bắt buộc");
        return section
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(text => text.Length > 0)
            .ToList();
    }

    /// <summary>Heuristic: ý chính coi là đã trả lời nếu transcript nhắc tới từ khoá đại diện của nó.</summary>
    private static bool Covers(string normalizedTranscript, string keyPoint)
    {
        var keywords = Normalize(keyPoint)
            .Split([' ', ',', ';', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 3)
            .ToArray();

        return keywords.Length > 0 && keywords.Any(normalizedTranscript.Contains);
    }

    /// <summary>
    /// Bỏ dấu tiếng Việt + hạ chữ thường để so khớp bền hơn với transcript từ STT
    /// (STT hay trả "dung" thay cho "dụng", "khop" thay cho "khớp").
    /// </summary>
    private static string Normalize(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        var withoutMarks = builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        // đ/Đ không tách được bằng Unicode decomposition.
        return withoutMarks.Replace('đ', 'd').Replace('Đ', 'D');
    }

    private static string NoFollowUp(string reason)
        => JsonSerializer.Serialize(new
        {
            needFollowUp = false,
            followUpQuestion = "",
            reason,
            coveredKeyPoints = Array.Empty<string>(),
            missingKeyPoints = Array.Empty<string>(),
            sources = Array.Empty<string>()
        });
}
