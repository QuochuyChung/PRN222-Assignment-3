using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service log lượt thi — chủ sở hữu: Dev B (LOG-01, LOG-02).</summary>
internal static class TurnLogServiceRegistration
{
    public static IServiceCollection AddTurnLogServices(this IServiceCollection services, InfraOptions infra)
    {
        // TODO LOG-01/02: services.AddScoped<ITurnLogService, TurnLogService>();

        if (infra.RunsWorker)
        {
            // TODO LOG-01: services.AddHostedService<TurnLogWorker>();
        }

        return services;
    }
}
