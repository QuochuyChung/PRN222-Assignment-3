using System.IO;
using System.Threading.Tasks;
using AiInterview.Infrastructure.Extractors;
using AiInterview.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiInterview.Tests;

public class OpenXmlDocumentTextExtractorTests
{
    private readonly OpenXmlDocumentTextExtractor _extractor;

    public OpenXmlDocumentTextExtractorTests()
    {
        _extractor = new OpenXmlDocumentTextExtractor(NullLogger<OpenXmlDocumentTextExtractor>.Instance);
    }

    [Fact]
    public async Task ExtractAsync_Docx_ShouldExtractParagraphsSuccessfully()
    {
        // Arrange
        string title = "Tong quan PRN222 va Lap trinh .NET";
        string content = "Mon hoc PRN222 giup sinh vien nam vung ASP.NET Core Razor Pages, Entity Framework Core, va microservices.";
        byte[] docxBytes = SampleFileGenerator.CreateSampleDocx(title, content);
        using var stream = new MemoryStream(docxBytes);

        // Act
        var result = await _extractor.ExtractAsync(stream, "bai_giang.docx");

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("bai_giang.docx");
        result.Pages.Should().NotBeEmpty();
        result.Pages[0].Content.Should().Contain(title);
        result.Pages[0].Content.Should().Contain(content);
    }

    [Fact]
    public async Task ExtractAsync_Pptx_ShouldExtractAllSlidesCorrectly()
    {
        // Arrange
        string slide1Title = "Chuong 1: Kien truc AI Interview";
        string slide1Content = "He thong bao gom Web, Worker, Redis Stream, Qdrant Vector Store va MinIO.";
        string slide2Title = "Chuong 2: Quy trinh Ingestion";
        string slide2Content = "Trich xuat text, phan doan chunk, embedding va upsert idempotent.";

        byte[] pptxBytes = SampleFileGenerator.CreateSamplePptx(slide1Title, slide1Content, slide2Title, slide2Content);
        using var stream = new MemoryStream(pptxBytes);

        // Act
        var result = await _extractor.ExtractAsync(stream, "presentation.pptx");

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().HaveCount(2);

        result.Pages[0].PageNumber.Should().Be(1);
        result.Pages[0].Content.Should().Contain(slide1Title);
        result.Pages[0].Content.Should().Contain(slide1Content);

        result.Pages[1].PageNumber.Should().Be(2);
        result.Pages[1].Content.Should().Contain(slide2Title);
        result.Pages[1].Content.Should().Contain(slide2Content);
    }
}
