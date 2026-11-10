using System;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AiInterview.Web.Services;

public class SignalRDocumentProgressNotifier : IDocumentProgressNotifier
{
    private readonly IHubContext<DocumentHub> _hubContext;
    private readonly ILogger<SignalRDocumentProgressNotifier> _logger;

    public SignalRDocumentProgressNotifier(
        IHubContext<DocumentHub> hubContext,
        ILogger<SignalRDocumentProgressNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyStatusChangedAsync(
        string documentId,
        string status,
        string? errorMessage,
        int chunkCount,
        int pageCount)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("DocumentStatusChanged", new
            {
                documentId,
                status,
                errorMessage,
                chunkCount,
                pageCount,
                updatedAt = DateTime.UtcNow
            });

            _logger.LogInformation("SignalRDocumentProgressNotifier.cs - SignalR: Pushed status update, documentId={DocId}, status={Status}",
                documentId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalRDocumentProgressNotifier.cs - SignalR: Failed to push status update, documentId={DocId}", documentId);
        }
    }

    public async Task NotifyProgressAsync(string documentId, string step, int percent)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("DocumentProgress", new
            {
                documentId,
                step,
                percent,
                updatedAt = DateTime.UtcNow
            });

            _logger.LogInformation("SignalRDocumentProgressNotifier.cs - SignalR: Pushed progress update, documentId={DocId}, step={Step}, percent={Percent}%",
                documentId, step, percent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalRDocumentProgressNotifier.cs - SignalR: Failed to push progress update, documentId={DocId}", documentId);
        }
    }
}

