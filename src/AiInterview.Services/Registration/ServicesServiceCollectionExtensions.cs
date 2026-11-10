using AiInterview.Common.Configuration;
using AiInterview.Data.Registration;
using AiInterview.Data.Seeding;
using AiInterview.Services.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>
/// Điểm vào duy nhất của Web để ráp toàn bộ ứng dụng. Services tự đăng ký lớp Data bên trong,
/// nên Web không cần (và không thể) tham chiếu Data.
/// </summary>
public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký lớp Repository (Fake/Real theo <see cref="InfraOptions.Mode"/>) và toàn bộ Service.
    /// Service giống nhau ở cả Fake và Real; khác biệt nằm hoàn toàn ở lớp Repository.
    /// Mỗi module có file đăng ký riêng để tránh đụng nhau khi merge.
    /// </summary>
    public static IServiceCollection AddServices(
        this IServiceCollection services,
        IConfiguration configuration,
        InfraOptions infra)
    {
        services.AddData(configuration, infra);

        // Service là Scoped vì phụ thuộc repository SQL (DbContext là Scoped).
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddIngestionServices(infra);
        services.AddQuestionServices(infra);
        services.AddEngineServices(infra);
        services.AddTurnLogServices(infra);

        return services;
    }

    /// <summary>Khởi tạo dữ liệu lúc khởi động: Fake tạo schema + seed; Real chỉ migrate khi <c>Database:AutoMigrate=true</c>.</summary>
    public static Task InitializeApplicationAsync(
        this IServiceProvider services,
        InfraOptions infra,
        IConfiguration configuration,
        CancellationToken ct = default)
        => services.InitializeDatabaseAsync(infra, configuration, ct);
}
