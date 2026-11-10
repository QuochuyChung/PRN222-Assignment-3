using AiInterview.Common.Configuration;
using AiInterview.Services.Interview;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service của module Interview Engine — chủ sở hữu: Dev C.</summary>
internal static class EngineServiceRegistration
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, InfraOptions infra)
    {
        // ENG-03 (state machine) và ENG-05 (luật cứng) là logic nội bộ, dùng qua IInterviewSessionService.
        // TODO ENG-02/03/05/06/07/09: services.AddScoped<IInterviewSessionService, InterviewSessionService>();

        // TODO ENG-04: services.AddScoped<IFollowUpDecider, FollowUpDecider>();
        //   Chờ QST-02: ILlmRepository chưa được đăng ký trong SharedRepositoryRegistration,
        //   đăng ký trước sẽ làm mọi lần resolve IInterviewSessionService ném InvalidOperationException.

        services.AddScoped<ITurnPublisher, TurnPublisher>();
        return services;
    }
}
