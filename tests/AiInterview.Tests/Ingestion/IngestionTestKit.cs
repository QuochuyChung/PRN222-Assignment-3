using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Ai;
using AiInterview.Data.Repositories.Documents;
using AiInterview.Data.Repositories.Files;
using AiInterview.Data.Repositories.Queue;
using AiInterview.Data.Repositories.Vectors;
using AiInterview.Services.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Tests.Ingestion;

/// <summary>Builds the ingestion pipeline with the Fake repositories, wired like the real DI setup.</summary>
internal sealed class IngestionTestKit
{
    public FakeDocumentRepository Documents { get; } = new();
    public FakeFileRepository Files { get; } = new();
    public InMemoryVectorRepository Vectors { get; } = new();
    public InMemoryMessageQueueRepository Queue { get; } = new();
    public IngestionOptions Options { get; } = new() { MaxAttempts = 3, RetryBaseDelayMs = 5, PollIntervalMs = 20, ChunkSize = 300, ChunkOverlap = 50 };
    public ServiceProvider Provider { get; }

    public IngestionTestKit(IEmbeddingRepository? embeddings = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDocumentRepository>(Documents);
        services.AddSingleton<IFileRepository>(Files);
        services.AddSingleton<IVectorRepository>(Vectors);
        services.AddSingleton<IMessageQueueRepository>(Queue);
        services.AddSingleton(embeddings ?? new FakeEmbeddingRepository(Microsoft.Extensions.Options.Options.Create(new GeminiOptions { EmbeddingDimensions = 64 })));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddSingleton<IDocumentProgressNotifier, NullDocumentProgressNotifier>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<IChunker, Chunker>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        Provider = services.BuildServiceProvider();
    }

    public IDocumentService Service => Provider.GetRequiredService<IDocumentService>();

    public DocumentWorker CreateWorker() => new(
        Provider.GetRequiredService<IServiceScopeFactory>(), Queue,
        Microsoft.Extensions.Options.Options.Create(Options), NullLogger<DocumentWorker>.Instance);

    public static string Seed(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiInterview.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "seed", "ingestion", name);
    }

    public async Task<DocumentDto> UploadSeedAsync(string name)
    {
        var bytes = await File.ReadAllBytesAsync(Seed(name));
        return await Service.UploadAsync(new UploadDocumentRequest(name, "application/octet-stream", new MemoryStream(bytes), bytes.Length, "test"));
    }

    /// <summary>Runs the worker until <paramref name="done"/> holds (or 10 s pass), then stops it.</summary>
    public async Task RunWorkerUntilAsync(Func<Task<bool>> done)
    {
        var worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await done() && DateTime.UtcNow < deadline) await Task.Delay(20);
        await worker.StopAsync(CancellationToken.None);
    }
}
