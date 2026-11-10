using System.IO;
using System.Threading.Tasks;
using AiInterview.Core.Exceptions;
using AiInterview.Infrastructure.Extractors;
using AiInterview.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiInterview.Tests;

public class PdfPigDocumentTextExtractorTests
{
    private readonly PdfPigDocumentTextExtractor _extractor;

    public PdfPigDocumentTextExtractorTests()
    {
        _extractor = new PdfPigDocumentTextExtractor(NullLogger<PdfPigDocumentTextExtractor>.Instance);
    }

    [Fact]
    public async Task ExtractAsync_ValidPdfWithText_ShouldExtractSuccessfully()
    {
        // Arrange
        string sampleText = "Lap trinh he thong phan tan va RAG voi Qdrant.";
        byte[] pdfBytes = SampleFileGenerator.CreateSamplePdfWithText(sampleText);
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _extractor.ExtractAsync(stream, "document.pdf");

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().NotBeEmpty();
        result.Pages[0].Content.Should().Contain(sampleText);
    }

    [Fact]
    public async Task ExtractAsync_ScannedPdfWithoutText_ShouldThrowScannedPdfException()
    {
        // Arrange
        byte[] scannedPdfBytes = SampleFileGenerator.CreateScannedPdfWithoutText();
        using var stream = new MemoryStream(scannedPdfBytes);

        // Act & Assert
        var act = () => _extractor.ExtractAsync(stream, "scanned_doc.pdf");
        await act.Should().ThrowAsync<ScannedPdfException>()
            .WithMessage("*scan*");
    }
}
