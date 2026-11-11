using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AiInterview.BLL.Questions;
using AiInterview.BLL.Retrieval;

namespace AiInterview.BLL.Registration;

/// <summary>Đăng ký service của module Questions + Retrieval — chủ sở hữu: Dev B.</summary>
internal static class QuestionServiceRegistration
{
    public static IServiceCollection AddQuestionServices(this IServiceCollection services, InfraOptions infra)
    {
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<IQuestionGeneratorService, QuestionGeneratorService>();
        services.AddScoped<IQuestionService, QuestionService>();
        if (infra.IsFake) services.AddHostedService<QuestionSampleCorpusSeeder>();
        return services;
    }
}
