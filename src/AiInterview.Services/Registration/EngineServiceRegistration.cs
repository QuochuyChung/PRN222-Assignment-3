using AiInterview.Common.Configuration;
using AiInterview.Services.Interview;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service của module Interview Engine — chủ sở hữu: Dev C.</summary>
internal static class EngineServiceRegistration
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services, InfraOptions infra)
    {
        // ENG-04 (IFollowUpDecider) chưa đăng ký: ILlmRepository thuộc QST-02 và chưa có trong
        // SharedRepositoryRegistration. InterviewSessionService nhận IFollowUpDecider là tham số
        // *tuỳ chọn* nên vẫn resolve được; khi QST-02 xong chỉ cần bỏ comment dòng dưới.
        // TODO ENG-04: services.AddScoped<IFollowUpDecider, FollowUpDecider>();

        services.AddScoped<IInterviewSessionService, InterviewSessionService>();
        services.AddScoped<ITurnPublisher, TurnPublisher>();
        return services;
    }
}
