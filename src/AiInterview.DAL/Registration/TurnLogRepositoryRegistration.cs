using AiInterview.Common.Configuration;
using AiInterview.DAL.Repositories.Turns;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.DAL.Registration;

/// <summary>Đăng ký repository log lượt thi — chủ sở hữu: Dev B (LOG-01, LOG-02).</summary>
internal static class TurnLogRepositoryRegistration
{
    public static IServiceCollection AddTurnLogRepositories(this IServiceCollection services, InfraOptions infra)
    {
        services.AddScoped<ITurnRepository, TurnRepository>();
        return services;
    }
}
