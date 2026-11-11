using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiInterview.Common.Configuration;
using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.DAL.Repositories.Ai;

public sealed class GeminiLlmRepository(IHttpClientFactory clients, IOptions<GeminiOptions> options, ILogger<GeminiLlmRepository> logger) : ILlmRepository
{
    private const string File = "GeminiLlmRepository.cs";

    public async Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default)
    {
        var model = options.Value.Model;
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("Gemini:Model is not configured.");
        var client = clients.CreateClient(HttpClientNames.Gemini);
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await client.PostAsJsonAsync($"v1beta/models/{model}:generateContent", body, ct);
                if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 4)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
                    logger.Warn(File, ServiceNames.Gemini, "HTTP {Status}, retry {Attempt}/4 after {DelayMs}ms, model={Model}", (int)response.StatusCode, attempt, (int)delay.TotalMilliseconds, model);
                    await Task.Delay(delay, ct);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var candidates = json.RootElement.GetProperty("candidates");
                var result = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                if (string.IsNullOrWhiteSpace(result)) throw new InvalidOperationException("Gemini returned an empty response.");
                logger.Info(File, ServiceNames.Gemini, "Generated JSON, model={Model}, characters={Length}", model, result.Length);
                return result;
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null && attempt < 4)
            {
                logger.Warn(File, ServiceNames.Gemini, "Network error, retry {Attempt}/4, model={Model}", attempt, model);
                await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Error(ex, File, ServiceNames.Gemini, "Generation failed, model={Model}, attempt={Attempt}", model, attempt);
                throw;
            }
        }
    }
}
