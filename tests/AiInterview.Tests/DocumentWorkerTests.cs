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
using Moq;
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
        byte[] docx = SampleFileGenerator.CreateSampleDocx("Worker Test", "Sample content for the DocumentWorker test.");
        using var stream = new MemoryStream(docx);

        var docService = sp.GetRequiredService<IDocumentService>();
        var doc = await docService.UploadDocumentAsync(stream, "worker_sample.docx", "application/docx", docx.Length);

        // Run the worker until the document is processed
        var worker = new DocumentWorker(sp, NullLogger<DocumentWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        var processedDoc = await WaitForStatusAsync(repo, doc.Id, DocumentStatus.Success, TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        // Check if doc status changed to Success
        processedDoc.Should().NotBeNull();
        processedDoc!.Status.Should().Be(DocumentStatus.Success);
        processedDoc.ChunkCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Worker_OnRepeatedFailure_ShouldDeadLetterAndMarkDocumentFailed()
    {
        // Arrange: a document service that always throws
        var services = new ServiceCollection();
        var repo = new InMemoryDocumentRepository(NullLogger<InMemoryDocumentRepository>.Instance);
        var queue = new InMemoryDocumentStreamQueue(NullLogger<InMemoryDocumentStreamQueue>.Instance);

        var failingDocService = new Mock<IDocumentService>();
        failingDocService
            .Setup(s => s.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated transient network failure"));

        services.AddSingleton<IDocumentRepository>(repo);
        services.AddSingleton<IDocumentStreamConsumer>(queue);
        services.AddSingleton<IDocumentStreamPublisher>(queue);
        services.AddScoped<IDocumentService>(_ => failingDocService.Object);
        services.AddLogging();

        var sp = services.BuildServiceProvider();

        string docId = "doc_failing_123";
        await repo.CreateAsync(new DocumentMetadata
        {
            Id = docId,
            FileName = "fail.docx",
            Status = DocumentStatus.Processing
        });

        await queue.PublishAsync(new DocStreamMessage { DocumentId = docId, StorageKey = $"{docId}/fail.docx" });

        // Use a tiny retry delay so the test finishes quickly
        var workerOptions = Microsoft.Extensions.Options.Options.Create(new AiInterviewOptions
        {
            Worker = new WorkerOptions { MaxDeliveryAttempts = 3, RetryBaseDelayMs = 10 }
        });
        var worker = new DocumentWorker(sp, NullLogger<DocumentWorker>.Instance, workerOptions);

        // Act
        await worker.StartAsync(CancellationToken.None);
        var failedDoc = await WaitForStatusAsync(repo, docId, DocumentStatus.Failed, TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        failedDoc.Should().NotBeNull();
        failedDoc!.Status.Should().Be(DocumentStatus.Failed);
        failedDoc.ErrorMessage.Should().Contain("3 attempts");
        failingDocService.Verify(
            s => s.ProcessDocumentAsync(docId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        queue.DeadLetters.Should().ContainSingle(d => d.Envelope.Message.DocumentId == docId);
    }

    private static async Task<DocumentMetadata?> WaitForStatusAsync(
        IDocumentRepository repo, string docId, DocumentStatus expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        DocumentMetadata? doc = null;
        while (DateTime.UtcNow < deadline)
        {
            doc = await repo.GetByIdAsync(docId);
            if (doc?.Status == expected) return doc;
            await Task.Delay(50);
        }
        return doc;
    }
}
