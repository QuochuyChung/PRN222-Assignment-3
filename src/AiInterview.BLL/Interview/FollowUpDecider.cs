using System.Text;
using System.Text.Json;
using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.BLL.Retrieval;
using Microsoft.Extensions.Logging;

namespace AiInterview.BLL.Interview;

/// <summary>
/// ENG-04: dựng prompt, gọi <see cref="ILlmRepository"/>, parse JSON ở Phụ lục C.
/// Chỉ trả về kết luận của LLM; luật cứng (hết giờ, hết lượt xoáy) do ENG-05 áp sau, không tin hoàn toàn LLM.
/// </summary>
internal sealed class FollowUpDecider(ILlmRepository llm, ILogger<FollowUpDecider> logger) : IFollowUpDecider
{
    private const string File = "FollowUpDecider.cs";

    private const string SystemInstruction = """
        Bạn là giám khảo vấn đáp của một môn học đại học. Bạn chỉ đánh giá câu trả lời của sinh viên so với
        câu hỏi, đáp án mẫu, các ý chính và các đoạn tài liệu được cung cấp.

        Quy tắc:
        1. Chỉ dựa vào tài liệu và đáp án mẫu được cung cấp; không thêm kiến thức bên ngoài.
        2. Chỉ hỏi xoáy khi câu trả lời thiếu hoặc mơ hồ một ý chính quan trọng. Nếu đã đủ ý thì không hỏi xoáy.
        3. Câu hỏi xoáy phải ngắn (một câu), bám đúng ý còn thiếu, không hỏi lại y nguyên câu cũ.
        4. "reason" là lý do kỹ thuật để lưu log, không phải lời nói với sinh viên.
        5. Chỉ trả về JSON đúng schema, không kèm markdown, không giải thích thêm.
        """;

    public async Task<FollowUpDecision> DecideAsync(FollowUpRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            // Không có nội dung để đánh giá: hỏi xoáy thêm cũng vô nghĩa, luật cứng sẽ chuyển câu.
            logger.Warn(File, ServiceNames.App, "Transcript rỗng, bỏ qua gọi LLM, followUpCount={FollowUpCount}", request.FollowUpCount);
            return NoFollowUp("Sinh viên không trả lời (transcript rỗng).");
        }

        string raw;
        try
        {
            raw = await llm.GenerateJsonAsync(SystemInstruction, BuildUserPrompt(request), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Mạng đứt, hết hạn mức, key sai hoặc quá thời gian chờ không được làm hỏng lượt thi của thí sinh:
            // ghi log rồi coi như đủ ý để phiên vẫn chuyển câu. Khi chính người dùng hủy request (ct) thì để lỗi đi tiếp.
            logger.Error(ex, File, ServiceNames.Gemini,
                "Không gọi được LLM, coi như không cần hỏi xoáy để lượt thi vẫn tiếp tục, followUpCount={FollowUpCount}",
                request.FollowUpCount);
            return NoFollowUp("Không gọi được LLM, tạm coi như đủ ý.");
        }

        var decision = Parse(raw);

        logger.Info(File, ServiceNames.Gemini,
            "Kết luận hỏi xoáy, needFollowUp={NeedFollowUp}, followUpCount={FollowUpCount}/{Max}, missingKeyPoints={Missing}, transcript={TranscriptPreview}",
            decision.NeedFollowUp, request.FollowUpCount, request.MaxFollowUps,
            decision.MissingKeyPoints.Count, LogText.Preview(request.Transcript));

        return decision;
    }

    /// <summary>Prompt người dùng: câu hỏi, đáp án mẫu, ý chính, transcript và các đoạn tài liệu đã truy xuất.</summary>
    internal static string BuildUserPrompt(FollowUpRequest request)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("## Câu hỏi");
        prompt.AppendLine(request.QuestionText);
        prompt.AppendLine();

        prompt.AppendLine("## Đáp án mẫu");
        prompt.AppendLine(request.ReferenceAnswer);
        prompt.AppendLine();

        prompt.AppendLine("## Các ý chính bắt buộc");
        foreach (var point in request.KeyPoints)
        {
            prompt.AppendLine($"- {point}");
        }

        prompt.AppendLine();
        prompt.AppendLine("## Câu trả lời của sinh viên (transcript STT, có thể sai chính tả)");
        prompt.AppendLine(request.Transcript);
        prompt.AppendLine();

        if (request.ContextChunks.Count > 0)
        {
            prompt.AppendLine("## Đoạn tài liệu liên quan");
            foreach (var chunk in request.ContextChunks)
            {
                prompt.AppendLine($"[{chunk.Id}] (trang {chunk.Page}) {chunk.Text}");
                prompt.AppendLine();
            }
        }

        prompt.AppendLine("## Ngữ cảnh phiên");
        prompt.AppendLine($"Đây là lượt hỏi xoáy thứ {request.FollowUpCount + 1}, tối đa {request.MaxFollowUps} lượt.");
        prompt.AppendLine();
        prompt.AppendLine("## Định dạng trả về (chỉ JSON)");
        prompt.AppendLine("""
            {
              "needFollowUp": true,
              "followUpQuestion": "câu hỏi xoáy, để chuỗi rỗng nếu không cần",
              "reason": "lý do ngắn gọn",
              "coveredKeyPoints": ["ý đã trả lời được"],
              "missingKeyPoints": ["ý còn thiếu"],
              "sources": ["doc1:3"]
            }
            """);

        return prompt.ToString();
    }

    internal FollowUpDecision Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            logger.Warn(File, ServiceNames.Gemini, "LLM trả về rỗng, coi như không cần hỏi xoáy");
            return NoFollowUp("LLM trả về rỗng.");
        }

        // Gemini hay bọc JSON trong ```json ... ``` dù đã dặn: cắt hàng rào trước khi parse.
        var json = StripCodeFence(raw);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var needFollowUp = ReadBool(root, "needFollowUp");
            var question = ReadString(root, "followUpQuestion");
            var reason = ReadString(root, "reason");

            // needFollowUp=true nhưng không có câu hỏi thì không hỏi xoáy được: hạ xuống false.
            if (needFollowUp && string.IsNullOrWhiteSpace(question))
            {
                logger.Warn(File, ServiceNames.Gemini, "LLM yêu cầu hỏi xoáy nhưng không trả câu hỏi, hạ needFollowUp=false");
                needFollowUp = false;
            }

            return new FollowUpDecision(
                needFollowUp,
                needFollowUp ? question : null,
                string.IsNullOrWhiteSpace(reason) ? "(LLM không nêu lý do)" : reason,
                ReadStringList(root, "coveredKeyPoints"),
                ReadStringList(root, "missingKeyPoints"),
                ReadStringList(root, "sources"));
        }
        catch (JsonException ex)
        {
            // Không làm sập lượt thi vì LLM trả sai định dạng: coi như đủ ý và chuyển câu.
            logger.Error(ex, File, ServiceNames.Gemini, "JSON của LLM không hợp lệ, coi như không cần hỏi xoáy, raw={Raw}",
                LogText.Preview(raw, 200));
            return NoFollowUp("JSON của LLM không hợp lệ.");
        }
    }

    private static FollowUpDecision NoFollowUp(string reason)
        => new(false, null, reason, [], [], []);

    private static string StripCodeFence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewLine = text.IndexOf('\n');
        if (firstNewLine < 0)
        {
            return text;
        }

        var body = text[(firstNewLine + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing >= 0 ? body[..closing] : body).Trim();
    }

    private static bool ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static string ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static IReadOnlyList<string> ReadStringList(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
            {
                items.Add(text);
            }
        }

        return items;
    }
}
