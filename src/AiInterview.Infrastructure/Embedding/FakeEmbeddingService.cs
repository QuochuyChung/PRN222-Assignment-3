using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Infrastructure.Embedding;

public class FakeEmbeddingService : IEmbeddingService
{
    private readonly int _dimensions;
    private readonly ILogger<FakeEmbeddingService> _logger;

    public FakeEmbeddingService(
        IOptions<AiInterviewOptions> options,
        ILogger<FakeEmbeddingService> logger)
    {
        _dimensions = options.Value.Embedding?.Dimensions > 0 ? options.Value.Embedding.Dimensions : 768;
        _logger = logger;
    }

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var result = new List<float[]>(texts.Count);

        foreach (var text in texts)
        {
            float[] vector = GenerateDeterministicVector(text, _dimensions);
            result.Add(vector);
        }

        _logger.LogInformation("FakeEmbeddingService.cs - Gemini: Generated fake embeddings, count={Count}, dimensions={Dimensions}",
            texts.Count, _dimensions);

        return Task.FromResult<IReadOnlyList<float[]>>(result);
    }

    private static float[] GenerateDeterministicVector(string text, int dimensions)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        int seed = BitConverter.ToInt32(hash, 0);
        var rng = new Random(seed);

        var vector = new float[dimensions];
        double sumSquares = 0.0;

        for (int i = 0; i < dimensions; i++)
        {
            // Value in range [-1.0, 1.0]
            float val = (float)(rng.NextDouble() * 2.0 - 1.0);
            vector[i] = val;
            sumSquares += val * val;
        }

        // L2 Normalize
        double norm = Math.Sqrt(sumSquares);
        if (norm > 0)
        {
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] = (float)(vector[i] / norm);
            }
        }

        return vector;
    }
}

