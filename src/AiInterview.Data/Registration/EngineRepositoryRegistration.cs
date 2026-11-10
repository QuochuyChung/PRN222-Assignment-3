using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Sessions;
using AiInterview.Data.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Data.Registration;

/// <summary>Đăng ký repository của module Interview Engine — chủ sở hữu: Dev C.</summary>
internal static class EngineRepositoryRegistration
{
    public static IServiceCollection AddEngineRepositories(this IServiceCollection services, InfraOptions infra)
    {
        services.AddScoped<ISessionRepository, SessionRepository>();

        if (infra.IsFake)
        {
            // Singleton vì giữ trạng thái trong bộ nhớ (giống Redis dùng chung giữa các request).
            services.AddSingleton<ISessionStateRepository, InMemorySessionStateRepository>();
            services.AddSingleton<IQuestionUsageRepository, InMemoryQuestionUsageRepository>();

            // ENG-10: câu hỏi mẫu đã duyệt để tạo được phiên nhiều câu trên máy dev.
            services.AddScoped<IDataSeeder, EngineDataSeeder>();
        }
        else
        {
            services.AddSingleton<ISessionStateRepository, RedisSessionStateRepository>();
            services.AddSingleton<IQuestionUsageRepository, RedisQuestionUsageRepository>();
        }

        return services;
    }
}
