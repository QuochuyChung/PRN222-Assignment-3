using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service của module Interview Engine — chủ sở hữu: Dev C.</summary>
internal static class EngineServiceRegistration
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, InfraOptions infra)
    {
        // TODO ENG-02/03/05/06/07/09: services.AddScoped<IInterviewSessionService, InterviewSessionService>();
        // TODO ENG-04:                services.AddScoped<IFollowUpDecider, FollowUpDecider>();
        // TODO ENG-08:                services.AddScoped<ITurnPublisher, TurnPublisher>();
        return services;
    }
}
