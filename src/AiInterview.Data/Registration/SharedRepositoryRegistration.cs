using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Data.Registration;

/// <summary>
/// Repository dùng chung giữa nhiều module. Người nào cài đặt trước thì thêm đăng ký vào đây
/// (báo M nếu nhiều người cùng cần), tránh mỗi module tự đăng ký một bản riêng.
/// </summary>
internal static class SharedRepositoryRegistration
{
    public static IServiceCollection AddSharedRepositories(this IServiceCollection services, InfraOptions infra)
    {
        if (infra.IsFake)
        {
            // TODO ING-06 (Dev A): IVectorRepository      -> InMemoryVectorRepository
            // TODO ING-05 (Dev A): IEmbeddingRepository   -> FakeEmbeddingRepository
            // TODO QST-02 / ENG-04: ILlmRepository        -> FakeLlmRepository (trả JSON kịch bản mẫu)
            // TODO ING-07 / LOG-01: IMessageQueueRepository -> InMemoryMessageQueueRepository
        }
        else
        {
            // TODO ING-06 (Dev A): IVectorRepository      -> QdrantVectorRepository
            // TODO ING-05 (Dev A): IEmbeddingRepository   -> GeminiEmbeddingRepository
            // TODO QST-02 / ENG-04: ILlmRepository        -> GeminiLlmRepository
            // TODO ING-07 / LOG-01: IMessageQueueRepository -> RedisMessageQueueRepository
        }

        return services;
    }
}
