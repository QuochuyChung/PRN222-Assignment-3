using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiInterview.Common.Configuration;
using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.DAL.Repositories.Ai;

/// <summary>
/// Gemini <c>batchEmbedContents</c>. Batches the input, keeps a minimum gap between calls (rate limit)
/// and retries 429/5xx/network errors with exponential backoff. The API key is set as a header on the shared
/// <c>gemini</c> HttpClient, never in the URL.
/// </summary>
public sealed class GeminiEmbeddingRepository(
    IHttpClientFactory httpClientFactory,
    IOptions<GeminiOptions> options,
    ILogger<GeminiEmbeddingRepository> logger) : IEmbeddingRepository
{
    private const string File = "GeminiEmbeddingRepository.cs";

    // ponytail: fixed limits; move to GeminiOptions if the free-tier quota changes (Gemini allows up to 100 texts per batch).
    private const int BatchSize = 50;
    private const int MaxAttempts = 4;
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(250);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastCall = DateTime.MinValue;

    private readonly GeminiOptions _options = options.Value;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.EmbeddingModel))
            throw new InvalidOperationException("Gemini:EmbeddingModel is not configured.");

        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
            result.AddRange(await EmbedBatchAsync(batch, ct));
        return result;
    }

    private async Task<IReadOnlyList<float[]>> EmbedBatchAsync(string[] batch, CancellationToken ct)
    {
        var model = $"models/{_options.EmbeddingModel}";
        var body = new
        {
            requests = batch.Select(t => new
            {
                model,
                content = new { parts = new[] { new { text = t } } },
                outputDimensionality = _options.EmbeddingDimensions
            })
        };
        var client = httpClientFactory.CreateClient(HttpClientNames.Gemini);

        for (var attempt = 1; ; attempt++)
        {
            await WaitForSlotAsync(ct);
            var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
            try
            {
                using var response = await client.PostAsJsonAsync($"v1beta/{model}:batchEmbedContents", body, ct);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    if (attempt >= MaxAttempts) response.EnsureSuccessStatusCode();
                    delay = response.Headers.RetryAfter?.Delta ?? delay;
                    logger.Warn(File, ServiceNames.Gemini, "HTTP {Status}, retry {Attempt}/{Max} in {DelayMs}ms, texts={Count}",
                        (int)response.StatusCode, attempt, MaxAttempts, (int)delay.TotalMilliseconds, batch.Length);
                    await Task.Delay(delay, ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadFromJsonAsync<BatchResponse>(cancellationToken: ct);
                if (json?.Embeddings is not { } embeddings || embeddings.Count != batch.Length)
                    throw new InvalidOperationException($"Gemini returned {json?.Embeddings?.Count ?? 0} embeddings for {batch.Length} texts.");

                logger.Info(File, ServiceNames.Gemini, "Embedded batch, model={Model}, texts={Count}", _options.EmbeddingModel, batch.Length);
                return embeddings.Select(e => e.Values).ToList();
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts && ex.StatusCode is null)
            {
                logger.Warn(File, ServiceNames.Gemini, "Network error, retry {Attempt}/{Max} in {DelayMs}ms: {Error}",
                    attempt, MaxAttempts, (int)delay.TotalMilliseconds, ex.Message);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Error(ex, File, ServiceNames.Gemini, "Embedding failed, model={Model}, texts={Count}, attempt={Attempt}",
                    _options.EmbeddingModel, batch.Length, attempt);
                throw;
            }
        }
    }

    private static async Task WaitForSlotAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _lastCall + MinGap - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _lastCall = DateTime.UtcNow;
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed record BatchResponse([property: JsonPropertyName("embeddings")] List<Embedding>? Embeddings);

    private sealed record Embedding([property: JsonPropertyName("values")] float[] Values);
}
