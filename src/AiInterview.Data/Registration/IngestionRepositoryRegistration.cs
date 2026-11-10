using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Documents;
using AiInterview.Data.Repositories.Files;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Data.Registration;

/// <summary>Đăng ký repository của module Ingestion — chủ sở hữu: Dev A.</summary>
internal static class IngestionRepositoryRegistration
{
    public static IServiceCollection AddIngestionRepositories(this IServiceCollection services, InfraOptions infra)
    {
        if (infra.IsFake)
        {
            services.AddSingleton<IDocumentRepository, FakeDocumentRepository>();
            // TODO ING-01: services.AddSingleton<IFileRepository, FakeFileRepository>();
        }
        else
        {
            // TODO ING-02: services.AddSingleton<IDocumentRepository, MongoDocumentRepository>();
            // TODO ING-01: services.AddSingleton<IFileRepository, MinioFileRepository>();
        }

        return services;
    }
}
