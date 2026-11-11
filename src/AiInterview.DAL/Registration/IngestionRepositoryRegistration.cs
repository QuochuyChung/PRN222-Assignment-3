using AiInterview.Common.Configuration;
using AiInterview.DAL.Repositories.Documents;
using AiInterview.DAL.Repositories.Files;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.DAL.Registration;

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
