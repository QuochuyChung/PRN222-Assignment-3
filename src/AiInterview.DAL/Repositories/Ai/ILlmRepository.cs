namespace AiInterview.DAL.Repositories.Ai;

/// <summary>
/// Gọi LLM và trả về chuỗi JSON thô. Prompt và việc parse JSON thuộc lớp Service.
/// Real = Gemini (Dev B QST-02 / Dev C ENG-04 dùng chung, thống nhất với nhau). Fake = trả JSON kịch bản mẫu.
/// </summary>
public interface ILlmRepository
{
    Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default);
}
