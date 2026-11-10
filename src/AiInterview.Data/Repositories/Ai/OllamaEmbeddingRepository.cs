using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiInterview.Common.Configuration;
using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Data.Repositories.Ai;

/// <summary>
/// Embedding bằng Ollama (<c>POST api/embed</c>), chạy trong mạng nội bộ nên không cần API key và không giới hạn hạn mức.
/// Gửi theo lô, thử lại khi Ollama bận (5xx) hoặc mất kết nối, và kiểm tra số chiều trả về khớp
/// <see cref="GeminiOptions.EmbeddingDimensions"/> (cũng là số chiều của collection Qdrant).
/// </summary>
public sealed class OllamaEmbeddingRepository(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> ollama,
    IOptions<GeminiOptions> dimensions,
    ILogger<OllamaEmbeddingRepository> logger) : IEmbeddingRepository
{
    private const string File = "OllamaEmbeddingRepository.cs";

    // ponytail: giới hạn cố định; Ollama chạy CPU/GPU cục bộ nên lô nhỏ để tránh vượt thời gian chờ.
    private const int BatchSize = 32;
    private const int MaxAttempts = 3;

    private readonly OllamaOptions _options = ollama.Value;
    private readonly int _dimensions = dimensions.Value.EmbeddingDimensions;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.EmbeddingModel))
            throw new InvalidOperationException("Ollama:EmbeddingModel is not configured.");

        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
            result.AddRange(await EmbedBatchAsync(batch, ct));
        return result;
    }

    private async Task<IReadOnlyList<float[]>> EmbedBatchAsync(string[] batch, CancellationToken ct)
    {
        var body = new { model = _options.EmbeddingModel, input = batch };
        var client = httpClientFactory.CreateClient(HttpClientNames.Ollama);

        for (var attempt = 1; ; attempt++)
        {
            var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
            try
            {
                using var response = await client.PostAsJsonAsync("api/embed", body, ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Ollama has no model '{_options.EmbeddingModel}'. Run: ollama pull {_options.EmbeddingModel}");
                }

                if ((int)response.StatusCode >= 500)
                {
                    if (attempt >= MaxAttempts) response.EnsureSuccessStatusCode();
                    logger.Warn(File, ServiceNames.Ollama, "HTTP {Status}, retry {Attempt}/{Max} in {DelayMs}ms, model={Model}, texts={Count}",
                        (int)response.StatusCode, attempt, MaxAttempts, (int)delay.TotalMilliseconds, _options.EmbeddingModel, batch.Length);
                    await Task.Delay(delay, ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: ct);
                if (json?.Embeddings is not { } embeddings || embeddings.Count != batch.Length)
                    throw new InvalidOperationException($"Ollama returned {json?.Embeddings?.Count ?? 0} embeddings for {batch.Length} texts.");

                var wrong = embeddings.FirstOrDefault(e => e.Length != _dimensions);
                if (wrong is not null)
                {
                    throw new InvalidOperationException(
                        $"Ollama model '{_options.EmbeddingModel}' returns {wrong.Length}-dimensional vectors but Gemini:EmbeddingDimensions is {_dimensions}. " +
                        "Set Gemini:EmbeddingDimensions to the model's size and recreate the Qdrant collection.");
                }

                logger.Info(File, ServiceNames.Ollama, "Embedded batch, model={Model}, texts={Count}, dimensions={Dimensions}",
                    _options.EmbeddingModel, batch.Length, _dimensions);
                return embeddings;
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts && ex.StatusCode is null)
            {
                logger.Warn(File, ServiceNames.Ollama, "Network error, retry {Attempt}/{Max} in {DelayMs}ms: {Error}",
                    attempt, MaxAttempts, (int)delay.TotalMilliseconds, ex.Message);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Error(ex, File, ServiceNames.Ollama, "Embedding failed, model={Model}, texts={Count}, attempt={Attempt}",
                    _options.EmbeddingModel, batch.Length, attempt);
                throw;
            }
        }
    }

    private sealed record EmbedResponse([property: JsonPropertyName("embeddings")] List<float[]>? Embeddings);
}
