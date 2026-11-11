using AiInterview.Common.Configuration;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.BLL.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Tests.Ingestion;

public class ExtractorAndChunkerTests
{
    private readonly DocumentTextExtractor _extractor = new(NullLogger<DocumentTextExtractor>.Instance);

    private async Task<ExtractedDocument> Extract(string seed)
    {
        await using var file = File.OpenRead(IngestionTestKit.Seed(seed));
        return await _extractor.ExtractAsync(file, seed);
    }

    [Fact]
    public async Task Docx_Is_Split_At_Headings_And_Keeps_Vietnamese()
    {
        var doc = await Extract("sample.docx");

        Assert.Equal(4, doc.Pages.Count); // title + 3 headed sections
        Assert.StartsWith("1. Khái niệm", doc.Pages[1].Text);
        Assert.Contains("Dependency Injection", doc.Pages[3].Text);
    }

    [Fact]
    public async Task Pptx_Gives_One_Page_Per_Slide()
    {
        var doc = await Extract("sample.pptx");

        Assert.Equal(3, doc.Pages.Count);
        Assert.Equal(2, doc.Pages[1].Index);
        Assert.Contains("DbContext quản lý kết nối", doc.Pages[1].Text);
    }

    [Fact]
    public async Task Pdf_Gives_One_Page_Per_Pdf_Page()
    {
        var doc = await Extract("sample.pdf");

        Assert.Equal(2, doc.Pages.Count);
        Assert.Contains("Truy xuất tăng cường", doc.Pages[0].Text);
        Assert.Contains("dead-letter", doc.Pages[1].Text);
    }

    [Fact]
    public async Task Scanned_Pdf_Is_Rejected_With_A_Clear_Message()
    {
        var ex = await Assert.ThrowsAsync<DocumentRejectedException>(() => Extract("scanned_error.pdf"));
        Assert.Contains("scan", ex.Message);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("fake.pdf")]
    public async Task Unsupported_Or_Broken_Files_Are_Rejected(string name)
    {
        await Assert.ThrowsAsync<DocumentRejectedException>(() => _extractor.ExtractAsync(new MemoryStream("not a document"u8.ToArray()), name));
    }

    [Fact]
    public void Chunker_Respects_Size_Overlap_And_Page_Numbers()
    {
        var chunker = new Chunker(Options.Create(new IngestionOptions { ChunkSize = 200, ChunkOverlap = 40 }));
        var longText = string.Join(" ", Enumerable.Range(1, 120).Select(i => $"word{i}"));
        var doc = new ExtractedDocument("x.pdf", [new ExtractedPage(1, "Short first page."), new ExtractedPage(2, longText)]);

        var chunks = chunker.Chunk(doc);

        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.ChunkIndex));
        Assert.Equal("Short first page.", chunks[0].Text);
        Assert.Equal(1, chunks[0].Page);
        Assert.All(chunks.Skip(1), c => Assert.Equal(2, c.Page));
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 200));
        Assert.True(chunks.Count > 3);
        // Overlap: the last word of a chunk appears again in the next one, and no word is cut in half.
        var last = chunks[1].Text.Split(' ').Last();
        Assert.Contains(last, chunks[2].Text.Split(' '));
        Assert.Contains("word120", chunks[^1].Text);
    }

    [Fact]
    public async Task Fake_Embedding_Is_Stable_And_Rewards_Shared_Words()
    {
        var repo = new FakeEmbeddingRepository(Options.Create(new GeminiOptions { EmbeddingDimensions = 256 }));
        var v = await repo.EmbedAsync(["razor pages page model", "razor pages page model", "razor pages routing", "kubernetes helm chart"]);

        static float Dot(float[] a, float[] b) => a.Zip(b, (x, y) => x * y).Sum();
        Assert.Equal(v[0], v[1]);
        Assert.True(Dot(v[0], v[2]) > Dot(v[0], v[3]));
        Assert.Equal(1f, Dot(v[0], v[0]), 3);
    }
}
