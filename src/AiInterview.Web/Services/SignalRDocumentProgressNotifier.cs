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

            _logger.LogInformation("SignalRDocumentProgressNotifier.cs - SignalR: Đẩy thông báo trạng thái, documentId={DocId}, status={Status}",
                documentId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalRDocumentProgressNotifier.cs - SignalR: Đẩy thông báo trạng thái thất bại, documentId={DocId}", documentId);
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

            _logger.LogInformation("SignalRDocumentProgressNotifier.cs - SignalR: Đẩy tiến độ xử lý, documentId={DocId}, step={Step}, percent={Percent}%",
                documentId, step, percent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalRDocumentProgressNotifier.cs - SignalR: Đẩy tiến độ thất bại, documentId={DocId}", documentId);
        }
    }
}

