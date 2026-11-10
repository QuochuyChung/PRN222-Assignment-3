using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using AiInterview.Infrastructure.Chunking;
using AiInterview.Infrastructure.Embedding;
using AiInterview.Infrastructure.Extractors;
using AiInterview.Infrastructure.Queue;
using AiInterview.Infrastructure.Repositories;
using AiInterview.Infrastructure.Services;
using AiInterview.Infrastructure.Storage;
using AiInterview.Infrastructure.Vector;
using AiInterview.Tests.Helpers;
using AiInterview.Web.Workers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiInterview.Tests;

public class DocumentWorkerTests
{
    [Fact]
    public async Task Worker_ShouldProcessMessageFromStreamAndMarkSuccess()
    {
        // Setup DI container for Worker
        var services = new ServiceCollection();
        var appOptions = Microsoft.Extensions.Options.Options.Create(new AiInterviewOptions
        {
            Embedding = new EmbeddingOptions { Dimensions = 768 },
            Chunking = new ChunkingOptions { ChunkSize = 300, ChunkOverlap = 50 }
        });

        var storage = new FakeFileStorage(NullLogger<FakeFileStorage>.Instance);
        var repo = new InMemoryDocumentRepository(NullLogger<InMemoryDocumentRepository>.Instance);
        var queue = new InMemoryDocumentStreamQueue(NullLogger<InMemoryDocumentStreamQueue>.Instance);

        var openXml = new OpenXmlDocumentTextExtractor(NullLogger<OpenXmlDocumentTextExtractor>.Instance);
        var pdfPig = new PdfPigDocumentTextExtractor(NullLogger<PdfPigDocumentTextExtractor>.Instance);
        var extractor = new CompositeDocumentTextExtractor(openXml, pdfPig, NullLogger<CompositeDocumentTextExtractor>.Instance);
        var chunker = new SemanticChunker(appOptions, NullLogger<SemanticChunker>.Instance);
        var embedding = new FakeEmbeddingService(appOptions, NullLogger<FakeEmbeddingService>.Instance);
        var vectorStore = new InMemoryVectorStore(NullLogger<InMemoryVectorStore>.Instance);

        services.AddSingleton<IFileStorage>(storage);
        services.AddSingleton<IDocumentRepository>(repo);
        services.AddSingleton<IDocumentStreamPublisher>(queue);
        services.AddSingleton<IDocumentStreamConsumer>(queue);
        services.AddSingleton<IDocumentTextExtractor>(extractor);
        services.AddSingleton<IChunker>(chunker);
        services.AddSingleton<IEmbeddingService>(embedding);
        services.AddSingleton<IVectorStore>(vectorStore);
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddLogging();

        var sp = services.BuildServiceProvider();

        // Seed a document
        byte[] docx = SampleFileGenerator.CreateSampleDocx("Worker Test", "Noi dung test cho DocumentWorker.");
        using var stream = new MemoryStream(docx);

        var docService = sp.GetRequiredService<IDocumentService>();
        var doc = await docService.UploadDocumentAsync(stream, "worker_sample.docx", "application/docx", docx.Length);

        // Run worker for a brief moment with CancellationToken
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var worker = new DocumentWorker(sp, NullLogger<DocumentWorker>.Instance);

        await worker.StartAsync(cts.Token);
        await Task.Delay(1500);
        await worker.StopAsync(CancellationToken.None);

        // Check if doc status changed to Success
        var processedDoc = await repo.GetByIdAsync(doc.Id);
        processedDoc.Should().NotBeNull();
        processedDoc!.Status.Should().Be(DocumentStatus.Success);
        processedDoc.ChunkCount.Should().BeGreaterThan(0);
    }
}
