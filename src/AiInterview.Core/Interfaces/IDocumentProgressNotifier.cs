using System.Threading.Tasks;

namespace AiInterview.Core.Interfaces;

public interface IDocumentProgressNotifier
{
    Task NotifyStatusChangedAsync(string documentId, string status, string? errorMessage, int chunkCount, int pageCount);
    Task NotifyProgressAsync(string documentId, string step, int percent);
}

