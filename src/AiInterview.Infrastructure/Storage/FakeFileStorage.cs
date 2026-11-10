using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Storage;

public class FakeFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _storage = new();
    private readonly ILogger<FakeFileStorage> _logger;

    public FakeFileStorage(ILogger<FakeFileStorage> logger)
    {
        _logger = logger;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        byte[] bytes = ms.ToArray();
        _storage[key] = bytes;

        _logger.LogInformation("FakeFileStorage.cs - App: Saved file to fake storage, key={Key}, size={Size}B, contentType={ContentType}",
            key, bytes.Length, contentType);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        if (!_storage.TryGetValue(key, out var bytes))
        {
            throw new FileNotFoundException($"File not found in fake storage: {key}");
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _storage.TryRemove(key, out _);
        _logger.LogInformation("FakeFileStorage.cs - App: Deleted file from fake storage, key={Key}", key);
        return Task.CompletedTask;
    }
}

