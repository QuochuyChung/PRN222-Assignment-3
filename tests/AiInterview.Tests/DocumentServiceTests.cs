using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Exceptions;
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
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AiInterview.Tests;

public class DocumentServiceTests
{
    private readonly FakeFileStorage _fileStorage;
    private readonly InMemoryDocumentRepository _docRepo;
    private readonly InMemoryDocumentStreamQueue _streamQueue;
    private readonly CompositeDocumentTextExtractor _extractor;
    private readonly SemanticChunker _chunker;
    private readonly FakeEmbeddingService _embeddingService;
    private readonly InMemoryVectorStore _vectorStore;
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        var appOptions = Microsoft.Extensions.Options.Options.Create(new AiInterviewOptions
        {
            Embedding = new EmbeddingOptions { Dimensions = 768 },
            Chunking = new ChunkingOptions { ChunkSize = 300, ChunkOverlap = 50 }
        });

        _fileStorage = new FakeFileStorage(NullLogger<FakeFileStorage>.Instance);
        _docRepo = new InMemoryDocumentRepository(NullLogger<InMemoryDocumentRepository>.Instance);
        _streamQueue = new InMemoryDocumentStreamQueue(NullLogger<InMemoryDocumentStreamQueue>.Instance);

        var openXml = new OpenXmlDocumentTextExtractor(NullLogger<OpenXmlDocumentTextExtractor>.Instance);
        var pdfPig = new PdfPigDocumentTextExtractor(NullLogger<PdfPigDocumentTextExtractor>.Instance);
        _extractor = new CompositeDocumentTextExtractor(openXml, pdfPig, NullLogger<CompositeDocumentTextExtractor>.Instance);

        _chunker = new SemanticChunker(appOptions, NullLogger<SemanticChunker>.Instance);
        _embeddingService = new FakeEmbeddingService(appOptions, NullLogger<FakeEmbeddingService>.Instance);
        _vectorStore = new InMemoryVectorStore(NullLogger<InMemoryVectorStore>.Instance);

        _service = new DocumentService(
            _fileStorage,
            _docRepo,
            _streamQueue,
            _extractor,
            _chunker,
            _embeddingService,
            _vectorStore,
            NullLogger<DocumentService>.Instance);
    }

    [Fact]
    public async Task UploadDocumentAsync_ValidDocx_ShouldSaveFileMongoAndPublishStream()
    {
        // Arrange
        byte[] docxBytes = SampleFileGenerator.CreateSampleDocx("PRN222 Intro", "Noi dung khoa hoc ve ASP.NET Core.");
        using var stream = new MemoryStream(docxBytes);

        // Act
        var metadata = await _service.UploadDocumentAsync(stream, "bai_hoc_1.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docxBytes.Length);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Status.Should().Be(DocumentStatus.Processing);

        // Check repo
        var savedDoc = await _docRepo.GetByIdAsync(metadata.Id);
        savedDoc.Should().NotBeNull();
        savedDoc!.FileName.Should().Be("bai_hoc_1.docx");

        // Check storage
        using var readStream = await _fileStorage.OpenReadAsync(metadata.StorageKey);
        readStream.Length.Should().Be(docxBytes.Length);

        // Check stream
        var messages = await _streamQueue.ReadMessagesAsync(1, TimeSpan.FromSeconds(1));
        messages.Should().HaveCount(1);
        messages[0].Message.DocumentId.Should().Be(metadata.Id);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ValidDocx_ShouldExtractChunkEmbedAndUpsertVectorStore()
    {
        // Arrange
        byte[] docxBytes = SampleFileGenerator.CreateSampleDocx("Tieu de bai giang", "Doan van mau ve lap trinh web hien dai.");
        using var stream = new MemoryStream(docxBytes);
        var metadata = await _service.UploadDocumentAsync(stream, "test_process.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docxBytes.Length);

        // Act
        await _service.ProcessDocumentAsync(metadata.Id, metadata.StorageKey);

        // Assert
        var updated = await _docRepo.GetByIdAsync(metadata.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(DocumentStatus.Success);
        updated.ChunkCount.Should().BeGreaterThan(0);
        updated.PageCount.Should().BeGreaterThan(0);

        // Verify search in vector store
        var dummyVector = new float[768];
        var searchHits = await _vectorStore.SearchAsync(dummyVector, topK: 5, new VectorFilter { DocumentId = metadata.Id });
        searchHits.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessDocumentAsync_ScannedPdf_ShouldMarkStatusFailedWithErrorMessage()
    {
        // Arrange
        byte[] pdfBytes = SampleFileGenerator.CreateScannedPdfWithoutText();
        using var stream = new MemoryStream(pdfBytes);
        var metadata = await _service.UploadDocumentAsync(stream, "scan_error.pdf", "application/pdf", pdfBytes.Length);

        // Act
        await _service.ProcessDocumentAsync(metadata.Id, metadata.StorageKey);

        // Assert
        var doc = await _docRepo.GetByIdAsync(metadata.Id);
        doc.Should().NotBeNull();
        doc!.Status.Should().Be(DocumentStatus.Failed);
        doc.ErrorMessage.Should().Contain("scan");
    }

    [Fact]
    public async Task DeleteDocumentAsync_ShouldRemoveFromStorageVectorStoreAndMongo()
    {
        // Arrange
        byte[] docxBytes = SampleFileGenerator.CreateSampleDocx("Xoa tai lieu", "Noi dung sap xoa.");
        using var stream = new MemoryStream(docxBytes);
        var metadata = await _service.UploadDocumentAsync(stream, "to_delete.docx", "application/docx", docxBytes.Length);
        await _service.ProcessDocumentAsync(metadata.Id, metadata.StorageKey);

        // Act
        await _service.DeleteDocumentAsync(metadata.Id);

        // Assert
        var doc = await _docRepo.GetByIdAsync(metadata.Id);
        doc.Should().BeNull();

        var searchHits = await _vectorStore.SearchAsync(new float[768], 5, new VectorFilter { DocumentId = metadata.Id });
        searchHits.Should().BeEmpty();
    }
}
