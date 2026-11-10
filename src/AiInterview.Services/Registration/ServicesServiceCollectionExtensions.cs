using AiInterview.Common.Configuration;
using AiInterview.Services.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>
/// Điểm đăng ký DI của lớp Service. Service giống nhau ở cả Fake và Real
/// (khác biệt Fake/Real nằm hoàn toàn ở lớp Repository).
/// Mỗi module có file đăng ký riêng để tránh đụng nhau khi merge.
/// </summary>
public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, InfraOptions infra)
    {
        // Service là Scoped vì phụ thuộc repository SQL (DbContext là Scoped).
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddIngestionServices(infra);
        services.AddQuestionServices(infra);
        services.AddEngineServices(infra);
        services.AddTurnLogServices(infra);

        return services;
    }
}
