using AiInterview.Common.Logging;
using AiInterview.BLL.Documents;
using Microsoft.AspNetCore.SignalR;

namespace AiInterview.Web.Hubs;

/// <summary>Push-only hub for the Documents page. Events: <c>progress</c> (DocumentProgress) and <c>deleted</c> (documentId).</summary>
public sealed class DocumentHub : Hub;

/// <summary>Sends pipeline progress to every connected Documents page.</summary>
public sealed class SignalRDocumentProgressNotifier(IHubContext<DocumentHub> hub, ILogger<SignalRDocumentProgressNotifier> logger)
    : IDocumentProgressNotifier
{
    private const string File = "SignalRDocumentProgressNotifier.cs";

    public Task NotifyAsync(DocumentProgress progress, CancellationToken ct = default)
        => Send("progress", progress, progress.DocumentId, ct);

    public Task NotifyDeletedAsync(string documentId, CancellationToken ct = default)
        => Send("deleted", documentId, documentId, ct);

    private async Task Send(string method, object payload, string documentId, CancellationToken ct)
    {
        try
        {
            await hub.Clients.All.SendAsync(method, payload, ct);
        }
        catch (Exception ex)
        {
            // Progress is best effort: the page also polls, so never fail the pipeline because of SignalR.
            logger.Error(ex, File, ServiceNames.SignalR, "Push failed, method={Method}, documentId={DocId}", method, documentId);
        }
    }
}
