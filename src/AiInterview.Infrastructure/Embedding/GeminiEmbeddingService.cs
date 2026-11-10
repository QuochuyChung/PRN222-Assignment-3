using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Infrastructure.Embedding;

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<GeminiEmbeddingService> _logger;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;

    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<AiInterviewOptions> options,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.Embedding ?? new EmbeddingOptions();
        _logger = logger;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();

        var results = new List<float[]>(texts.Count);
        int batchSize = Math.Max(1, _options.BatchSize);

        // Process in batches
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            var batchEmbeddings = await EmbedBatchWithRetryAsync(batch, ct);
            results.AddRange(batchEmbeddings);
        }

        return results;
    }

    private async Task<IReadOnlyList<float[]>> EmbedBatchWithRetryAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        string model = _options.ModelName;
        string? apiKey = _options.ApiKey;
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:batchEmbedContents?key={apiKey}";

        int maxRetries = Math.Max(1, _options.MaxRetries);
        int backoffMs = Math.Max(200, _options.InitialBackoffMs);

        var payload = new
        {
            requests = texts.Select(t => new
            {
                model = $"models/{model}",
                content = new
                {
                    parts = new[] { new { text = t } }
                }
            }).ToArray()
        };

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _rateLimiter.WaitAsync(ct);
            try
            {
                // Ensure at least 200ms between requests to avoid burst rate-limiting
                var timeSinceLast = DateTime.UtcNow - _lastRequestTime;
                if (timeSinceLast < TimeSpan.FromMilliseconds(200))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200) - timeSinceLast, ct);
                }
                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _rateLimiter.Release();
            }

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    _logger.LogWarning("GeminiEmbeddingService.cs - Gemini: Bị giới hạn tốc độ hoặc lỗi máy chủ HTTP {StatusCode}, thử lại lần {Attempt}/{Max} sau {DelayMs}ms",
                        response.StatusCode, attempt, maxRetries, backoffMs);

                    if (attempt == maxRetries)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    await Task.Delay(backoffMs, ct);
                    backoffMs *= 2;
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var jsonResult = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>(cancellationToken: ct);
                if (jsonResult?.Embeddings == null || jsonResult.Embeddings.Count == 0)
                {
                    throw new InvalidOperationException("Gemini returned empty embeddings array.");
                }

                var batchVectors = jsonResult.Embeddings.Select(e => e.Values).ToList();
                _logger.LogInformation("GeminiEmbeddingService.cs - Gemini: Embed batch thành công, số văn bản={Count}, model={Model}",
                    texts.Count, model);

                return batchVectors;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "GeminiEmbeddingService.cs - Gemini: Lỗi kết nối HTTP, thử lại lần {Attempt}/{Max} sau {DelayMs}ms",
                    attempt, maxRetries, backoffMs);
                await Task.Delay(backoffMs, ct);
                backoffMs *= 2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeminiEmbeddingService.cs - Gemini: Embed thất bại sau {Attempt} lần thử, model={Model}",
                    attempt, model);
                throw;
            }
        }

        throw new InvalidOperationException($"Không thể tạo embedding từ Gemini sau {maxRetries} lần thử.");
    }

    private class GeminiBatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<GeminiEmbeddingItem> Embeddings { get; set; } = new();
    }

    private class GeminiEmbeddingItem
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}

