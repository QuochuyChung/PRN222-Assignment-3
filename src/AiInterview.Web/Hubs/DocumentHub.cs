using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AiInterview.Web.Hubs;

public class DocumentHub : Hub
{
    private readonly ILogger<DocumentHub> _logger;

    public DocumentHub(ILogger<DocumentHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinDocumentGroup(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doc_{documentId}");
        _logger.LogInformation("DocumentHub.cs - SignalR: Client {ConnectionId} subscribed to document {DocumentId}",
            Context.ConnectionId, documentId);
    }

    public async Task LeaveDocumentGroup(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doc_{documentId}");
    }
}

