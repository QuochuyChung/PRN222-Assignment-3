using System;
using System.Threading.Tasks;
using AiInterview.Core.Options;
using AiInterview.Infrastructure.Embedding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiInterview.Tests;

public class FakeEmbeddingServiceTests
{
    private readonly FakeEmbeddingService _embeddingService;

    public FakeEmbeddingServiceTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AiInterviewOptions
        {
            Embedding = new EmbeddingOptions
            {
                Dimensions = 768
            }
        });

        _embeddingService = new FakeEmbeddingService(options, NullLogger<FakeEmbeddingService>.Instance);
    }

    [Fact]
    public async Task EmbedAsync_ShouldGenerateExactDimensionsAndL2NormalizedVector()
    {
        // Arrange
        var texts = new[] { "Khai niem Dependency Injection trong ASP.NET Core" };

        // Act
        var result = await _embeddingService.EmbedAsync(texts);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().HaveCount(768);

        // Check L2 normalization: sum of squares should be approximately 1.0
        double sumSquares = 0.0;
        foreach (var val in result[0])
        {
            sumSquares += val * val;
        }
        sumSquares.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task EmbedAsync_SameText_ShouldProduceDeterministicIdenticalVector()
    {
        // Arrange
        string text = "Qdrant Vector Database";

        // Act
        var result1 = await _embeddingService.EmbedAsync(new[] { text });
        var result2 = await _embeddingService.EmbedAsync(new[] { text });

        // Assert
        result1[0].Should().Equal(result2[0]);
    }
}

