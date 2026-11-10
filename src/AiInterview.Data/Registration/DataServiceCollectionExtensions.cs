using AiInterview.Common.Configuration;
using AiInterview.Data.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Data.Registration;

/// <summary>
/// Điểm đăng ký DI của lớp Data (Repository). Chọn Fake/Real theo <see cref="InfraOptions.Mode"/>.
/// Mỗi module có file đăng ký riêng (xem các class <c>*RepositoryRegistration</c>) để tránh đụng nhau khi merge.
/// </summary>
public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration, InfraOptions infra)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<InterviewOptions>(configuration.GetSection(InterviewOptions.SectionName));

        services.AddSqlDatabase(configuration, infra);

        // Readiness probe: kết nối được SQL. Đăng ký ở lớp Data để Web không phải biết AppDbContext.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("sql", tags: new[] { "ready" });

        if (infra.IsReal)
        {
            services.AddSharedClients();
        }
        else
        {
            services.AddScoped<IDataSeeder, SampleDataSeeder>();
        }

        services.AddSharedRepositories(infra);
        services.AddIngestionRepositories(infra);
        services.AddQuestionRepositories(infra);
        services.AddEngineRepositories(infra);
        services.AddTurnLogRepositories(infra);

        return services;
    }

    private static IServiceCollection AddSqlDatabase(this IServiceCollection services, IConfiguration configuration, InfraOptions infra)
    {
        if (infra.IsFake)
        {
            // SQLite in-memory: giữ kết nối mở suốt vòng đời app thì DB mới không bị mất.
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            services.AddSingleton(connection);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("SqlServer");
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        }

        return services;
    }
}
