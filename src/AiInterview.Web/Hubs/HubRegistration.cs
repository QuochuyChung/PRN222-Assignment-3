using AiInterview.BLL.Documents;

namespace AiInterview.Web.Hubs;

/// <summary>SignalR hubs of the app. Each hub adds its services and its route here.</summary>
public static class HubRegistration
{
    public static IServiceCollection AddHubServices(this IServiceCollection services)
    {
        // Registered after AddServices, so it replaces the no-op notifier from the BLL layer.
        services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentProgressNotifier>();
        return services;
    }

    public static IEndpointRouteBuilder MapHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<DocumentHub>("/hubs/documents");
        return endpoints;
    }
}
