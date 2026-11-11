namespace AiInterview.DAL.Repositories.Ai;

/// <summary>Gọi API embedding. Real = Gemini (Dev A, ING-05): gộp batch, giới hạn tốc độ, retry có backoff.</summary>
public interface IEmbeddingRepository
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
