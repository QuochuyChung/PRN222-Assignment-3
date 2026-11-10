using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AiInterview.Core.Interfaces;

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

