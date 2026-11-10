using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiInterview.Data.Repositories.Ai;

public sealed class FakeLlmRepository : ILlmRepository
{
    public Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default)
    {
        if (systemInstruction.Contains("QUESTION_GENERATION", StringComparison.Ordinal))
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
            return Task.FromResult(JsonSerializer.Serialize(Enumerable.Range(1, requested).Select(i => new
            {
                content = $"Câu {i}: Nêu ý chính của đoạn tài liệu được cung cấp?",
                referenceAnswer = "Trình bày chính xác nội dung và ví dụ từ đoạn tài liệu.",
                keyPoints = new[] { "Nội dung chính", "Ví dụ" }, bloomLevel = level.Success ? level.Groups[1].Value : "Understand", sourceChunkIds = ids.Take(1).ToArray()
            })));
        }
        return Task.FromResult("{\"needFollowUp\":false,\"followUpQuestion\":\"\",\"reason\":\"Đã đủ ý chính\",\"coveredKeyPoints\":[],\"missingKeyPoints\":[],\"sources\":[]}");
    }
}
