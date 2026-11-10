using AiInterview.Common.Configuration;
using AiInterview.Services.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Services.Registration;

/// <summary>Service registrations of the Ingestion module. Owner: Dev A.</summary>
internal static class IngestionServiceRegistration
{
    public static IServiceCollection AddIngestionServices(this IServiceCollection services, InfraOptions infra)
    {
        services.AddOptions<IngestionOptions>().BindConfiguration(IngestionOptions.SectionName);

        services.AddScoped<IDocumentService, DocumentService>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<IChunker, Chunker>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

        // Web replaces this with the SignalR notifier (registered later, so it wins).
        services.AddSingleton<IDocumentProgressNotifier, NullDocumentProgressNotifier>();

        if (infra.RunsWorker)
        {
            // The worker is a singleton: it resolves scoped services through IServiceScopeFactory.CreateScope().
            services.AddHostedService<DocumentWorker>();
        }

        return services;
    }
}
