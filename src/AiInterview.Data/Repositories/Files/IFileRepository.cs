namespace AiInterview.Data.Repositories.Files;

/// <summary>Original document files. Real = MinIO (Dev A, ING-01). Key: <c>{documentId}/{fileName}</c>.</summary>
public interface IFileRepository
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
