using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service của module Questions + Retrieval — chủ sở hữu: Dev B.</summary>
internal static class QuestionServiceRegistration
{
    public static IServiceCollection AddQuestionServices(this IServiceCollection services, InfraOptions infra)
    {
        // TODO QST-01: services.AddScoped<IRetrievalService, RetrievalService>();
        // TODO QST-02: services.AddScoped<IQuestionGeneratorService, QuestionGeneratorService>();
        // TODO QST-03: services.AddScoped<IQuestionService, QuestionService>();
        return services;
    }
}
