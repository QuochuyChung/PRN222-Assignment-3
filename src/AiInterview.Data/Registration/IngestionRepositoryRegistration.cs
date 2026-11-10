using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Documents;
using AiInterview.Data.Repositories.Files;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Data.Registration;

/// <summary>Repository registrations of the Ingestion module. Owner: Dev A.</summary>
internal static class IngestionRepositoryRegistration
{
    public static IServiceCollection AddIngestionRepositories(this IServiceCollection services, InfraOptions infra)
    {
        if (infra.IsFake)
        {
            services.AddSingleton<IDocumentRepository, FakeDocumentRepository>();
            services.AddSingleton<IFileRepository, FakeFileRepository>();
        }
        else
        {
            services.AddSingleton<IDocumentRepository, MongoDocumentRepository>();
            services.AddSingleton<IFileRepository, MinioFileRepository>();
        }

        return services;
    }
}
