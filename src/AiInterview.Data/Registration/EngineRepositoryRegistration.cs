using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Sessions;
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
            // TODO ENG-01: services.AddSingleton<ISessionStateRepository, InMemorySessionStateRepository>();
        }
        else
        {
            // TODO ENG-01: services.AddSingleton<ISessionStateRepository, RedisSessionStateRepository>();
        }

        // TODO ENG-10: services.AddScoped<IDataSeeder, EngineDataSeeder>(); (chỉ khi infra.IsFake)
        return services;
    }
}
