using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AiInterview.Services.TurnLog;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service log lượt thi — chủ sở hữu: Dev B (LOG-01, LOG-02).</summary>
internal static class TurnLogServiceRegistration
{
    public static IServiceCollection AddTurnLogServices(this IServiceCollection services, InfraOptions infra)
    {
        services.AddScoped<ITurnLogService, TurnLogService>();

        if (infra.RunsWorker)
        {
            services.AddHostedService<TurnLogWorker>();
        }

        return services;
    }
}
