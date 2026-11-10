using AiInterview.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Đăng ký service của module Ingestion — chủ sở hữu: Dev A.</summary>
internal static class IngestionServiceRegistration
{
    public static IServiceCollection AddIngestionServices(this IServiceCollection services, InfraOptions infra)
    {
        // TODO ING-02/08: services.AddScoped<IDocumentService, DocumentService>();
        // TODO ING-03:    services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        // TODO ING-04:    services.AddSingleton<IChunker, Chunker>();
        // TODO ING-07:    services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

        if (infra.RunsWorker)
        {
            // TODO ING-07: services.AddHostedService<DocumentWorker>();
            // Worker là singleton: lấy service Scoped qua IServiceScopeFactory.CreateScope().
        }

        return services;
    }
}
