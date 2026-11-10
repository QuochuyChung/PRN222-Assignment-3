using AiInterview.Common.Configuration;
using AiInterview.Services.Interview;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service của module Interview Engine — chủ sở hữu: Dev C.</summary>
internal static class EngineServiceRegistration
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, InfraOptions infra)
    {
        // ENG-04: ILlmRepository do QST-02 (Dev B) đăng ký trong QuestionRepositoryRegistration
        // nên ở đây chỉ cần nối IFollowUpDecider -> FollowUpDecider.
        services.AddScoped<IFollowUpDecider, FollowUpDecider>();

        services.AddScoped<IInterviewSessionService, InterviewSessionService>();
        services.AddScoped<ITurnPublisher, TurnPublisher>();
        return services;
    }
}
