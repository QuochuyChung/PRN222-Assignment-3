using System.Collections.Generic;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using AiInterview.Infrastructure.Chunking;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiInterview.Tests;

public class SemanticChunkerTests
{
    private readonly SemanticChunker _chunker;

    public SemanticChunkerTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AiInterviewOptions
        {
            Chunking = new ChunkingOptions
            {
                ChunkSize = 200,
                ChunkOverlap = 50
            }
        });

        _chunker = new SemanticChunker(options, NullLogger<SemanticChunker>.Instance);
    }

    [Fact]
    public void Chunk_SmallDocument_ShouldProduceSingleChunkWithCorrectMetadata()
    {
        // Arrange
        var doc = new ExtractedDocument
        {
            DocumentId = "doc_test_1",
            FileName = "test.docx",
            Pages = new List<ExtractedPage>
            {
                new()
                {
                    PageNumber = 1,
                    Title = "Page 1",
                    Content = "Noi dung ngan gon duoi 200 ky tu de test chunk don."
                }
            }
        };

        // Act
        var chunks = _chunker.Chunk(doc);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].DocumentId.Should().Be("doc_test_1");
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[0].Page.Should().Be(1);
        chunks[0].Text.Should().Be(doc.Pages[0].Content);
        chunks[0].Metadata.Should().ContainKey("page");
    }

    [Fact]
    public void Chunk_LongDocument_ShouldProduceMultipleOverlappingChunks()
    {
        // Arrange
        string longText = new string('A', 450); // 450 characters with chunkSize=200, overlap=50 (step=150)
        var doc = new ExtractedDocument
        {
            DocumentId = "doc_test_2",
            FileName = "long.docx",
            Pages = new List<ExtractedPage>
            {
                new()
                {
                    PageNumber = 1,
                    Content = longText
                }
            }
        };

        // Act
        var chunks = _chunker.Chunk(doc);

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].ChunkIndex.Should().Be(i);
            chunks[i].Page.Should().Be(1);
            chunks[i].DocumentId.Should().Be("doc_test_2");
        }
    }
}

