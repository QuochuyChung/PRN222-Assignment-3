using System.Collections.Concurrent;

namespace AiInterview.Data.Repositories.Files;

/// <summary>Fake file store kept in memory (singleton). Files are lost when the app stops.</summary>
public sealed class FakeFileRepository : IFileRepository
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _files[key] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
        => _files.TryGetValue(key, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new FileNotFoundException($"File not found in fake storage: {key}");

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _files.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
