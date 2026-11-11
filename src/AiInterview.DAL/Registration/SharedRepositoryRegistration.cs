using AiInterview.Common.Configuration;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.DAL.Repositories.Queue;
using AiInterview.DAL.Repositories.Vectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiInterview.DAL.Registration;

/// <summary>
/// Repositories shared by several modules. Whoever implements one first registers it here
/// (tell M if several people need it), so modules do not each register their own copy.
/// </summary>
internal static class SharedRepositoryRegistration
{
    public static IServiceCollection AddSharedRepositories(this IServiceCollection services, InfraOptions infra)
    {
        if (infra.IsFake)
        {
            services.AddSingleton<IVectorRepository, InMemoryVectorRepository>();
            services.AddSingleton<IEmbeddingRepository, FakeEmbeddingRepository>();
            services.AddSingleton<IMessageQueueRepository, InMemoryMessageQueueRepository>();
            // ILlmRepository do module Questions đăng ký (QuestionRepositoryRegistration) — QST-02.
        }
        else
        {
            services.AddSingleton<IVectorRepository, QdrantVectorRepository>();
            // Embedding: chọn bằng Embedding:Provider (Ollama mặc định, hoặc Gemini). Sinh văn bản vẫn dùng Gemini.
            services.AddSingleton<GeminiEmbeddingRepository>();
            services.AddSingleton<OllamaEmbeddingRepository>();
            services.AddSingleton<IEmbeddingRepository>(sp =>
                sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value.IsGemini
                    ? sp.GetRequiredService<GeminiEmbeddingRepository>()
                    : sp.GetRequiredService<OllamaEmbeddingRepository>());
            services.AddSingleton<IMessageQueueRepository, RedisMessageQueueRepository>();
            // ILlmRepository do module Questions đăng ký (QuestionRepositoryRegistration) — QST-02.
        }

        return services;
    }
}
