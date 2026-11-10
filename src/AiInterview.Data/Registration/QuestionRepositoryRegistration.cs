using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Questions;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Data.Registration;

/// <summary>Đăng ký repository của module Questions — chủ sở hữu: Dev B.</summary>
internal static class QuestionRepositoryRegistration
{
    public static IServiceCollection AddQuestionRepositories(this IServiceCollection services, InfraOptions infra)
    {
        // SQL dùng chung cho cả Fake (SQLite in-memory) và Real (SQL Server).
        services.AddScoped<IQuestionRepository, QuestionRepository>();

        // TODO QST-06: services.AddScoped<IDataSeeder, QuestionDataSeeder>(); (chỉ khi infra.IsFake)
        return services;
    }
}
