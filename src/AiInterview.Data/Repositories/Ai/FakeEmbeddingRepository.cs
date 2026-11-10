using System.Security.Cryptography;
using System.Text;
using AiInterview.Common.Configuration;
using Microsoft.Extensions.Options;

namespace AiInterview.Data.Repositories.Ai;

/// <summary>
/// Fake embedding: hashes every word into one dimension (feature hashing), then L2-normalizes.
/// Same text = same vector, and texts that share words get a higher cosine score, so retrieval still makes sense in Fake mode.
/// </summary>
public sealed class FakeEmbeddingRepository(IOptions<GeminiOptions> options) : IEmbeddingRepository
{
    private readonly int _dimensions = Math.Max(8, options.Value.EmbeddingDimensions);

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

    private float[] Embed(string text)
    {
        var vector = new float[_dimensions];
        var words = text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(word.Trim('.', ',', ';', ':', '!', '?', '(', ')', '"')));
            vector[BitConverter.ToUInt32(hash, 0) % (uint)_dimensions] += (hash[4] & 1) == 0 ? 1f : -1f;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }
}
