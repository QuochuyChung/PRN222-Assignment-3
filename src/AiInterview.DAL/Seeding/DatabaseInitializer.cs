using AiInterview.Common.Configuration;
using AiInterview.Common.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiInterview.DAL.Seeding;

public static class DatabaseInitializer
{
    /// <summary>
    /// Fake: tạo schema trong SQLite in-memory rồi chạy mọi <see cref="IDataSeeder"/>.
    /// Real: chỉ chạy migration khi <c>Database:AutoMigrate=true</c> (mặc định tắt, M chạy migration chủ động).
    /// </summary>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        InfraOptions infra,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (infra.IsFake)
        {
            await db.Database.EnsureCreatedAsync(ct);

            var seeders = scope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(s => s.Order).ToList();
            foreach (var seeder in seeders)
            {
                await seeder.SeedAsync(db, ct);
            }

            logger.Info("DatabaseInitializer.cs", ServiceNames.SqlServer,
                "Chế độ Fake: đã tạo SQLite in-memory và chạy {Count} seeder", seeders.Count);
            return;
        }

        if (configuration.GetValue<bool>("Database:AutoMigrate"))
        {
            await db.Database.MigrateAsync(ct);
            logger.Info("DatabaseInitializer.cs", ServiceNames.SqlServer, "Đã chạy migration (Database:AutoMigrate=true)");
        }
        else
        {
            logger.Info("DatabaseInitializer.cs", ServiceNames.SqlServer,
                "Bỏ qua migration tự động (Database:AutoMigrate=false)");
        }
    }
}
