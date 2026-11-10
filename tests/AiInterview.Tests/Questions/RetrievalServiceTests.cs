using AiInterview.Common.Configuration;
using AiInterview.Data.Models;
using AiInterview.Data.Repositories.Ai;
using AiInterview.Data.Repositories.Vectors;
using AiInterview.Services.Retrieval;
using Microsoft.Extensions.Options;

namespace AiInterview.Tests.Questions;

public sealed class RetrievalServiceTests
{
    private readonly FakeEmbeddingRepository _embeddings = new(Options.Create(new GeminiOptions { EmbeddingDimensions = 64 }));
    private readonly InMemoryVectorRepository _vectors = new();

    [Fact]
    public async Task Search_Uses_Embedding_And_Returns_Source_Metadata()
    {
        await AddAsync("doc-a", 0, 2, "Razor Pages uses PageModel and file based routing.");
        await AddAsync("doc-a", 1, 3, "Dependency injection connects services to repositories.");
        var service = new RetrievalService(_embeddings, _vectors);

        var hits = await service.SearchAsync("PageModel routing", new RetrievalScope(["doc-a"]), 2);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal("doc-a", h.DocumentId));
        Assert.All(hits, h => Assert.StartsWith("doc-a:", h.Id));
        Assert.Contains(hits, h => h.Page == 2 && h.Text.Contains("PageModel"));
        Assert.True(hits[0].Score >= hits[1].Score);
    }

    [Fact]
    public async Task Search_Respects_Document_Scope()
    {
        await AddAsync("doc-a", 0, 1, "Razor Pages PageModel");
        await AddAsync("doc-b", 0, 1, "Razor Pages PageModel");
        var service = new RetrievalService(_embeddings, _vectors);

        var hits = await service.SearchAsync("Razor Pages PageModel", new RetrievalScope(["doc-b"]), 10);

        var hit = Assert.Single(hits);
        Assert.Equal("doc-b", hit.DocumentId);
        Assert.Equal("doc-b:0", hit.Id);
    }

    [Fact]
    public async Task Search_Without_Document_Scope_Uses_All_Indexed_Documents()
    {
        await AddAsync("doc-a", 0, 1, "PageModel");
        await AddAsync("doc-b", 0, 1, "Repository");
        var service = new RetrievalService(_embeddings, _vectors);

        var hits = await service.SearchAsync("PageModel", new RetrievalScope(), 10);

        Assert.Equal(2, hits.Count);
        Assert.Equal(new[] { "doc-a", "doc-b" }, hits.Select(h => h.DocumentId).OrderBy(x => x));
    }

    [Fact]
    public async Task Search_TopK_Limits_Results()
    {
        await AddAsync("doc-a", 0, 1, "First passage");
        await AddAsync("doc-a", 1, 2, "Second passage");
        await AddAsync("doc-a", 2, 3, "Third passage");
        var service = new RetrievalService(_embeddings, _vectors);

        var hits = await service.SearchAsync("passage", new RetrievalScope(["doc-a"]), 1);

        Assert.Single(hits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_Rejects_Empty_Query_Before_Calling_Repositories(string query)
    {
        var service = new RetrievalService(new ThrowingEmbeddings(), _vectors);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(query, new RetrievalScope(), 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Search_Rejects_Nonpositive_TopK(int topK)
    {
        var service = new RetrievalService(new ThrowingEmbeddings(), _vectors);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync("query", new RetrievalScope(), topK));
    }

    [Fact]
    public async Task Search_Rejects_Embedding_Response_With_Wrong_Count()
    {
        var service = new RetrievalService(new EmptyEmbeddings(), _vectors);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchAsync("query", new RetrievalScope(), 3));
    }

    private async Task AddAsync(string documentId, int index, int page, string text)
    {
        var embedding = (await _embeddings.EmbedAsync([text]))[0];
        await _vectors.UpsertAsync([new VectorPoint($"{documentId}:{index}", embedding, documentId, index, page, text)]);
    }

    private sealed class ThrowingEmbeddings : IEmbeddingRepository
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => throw new InvalidOperationException("Embeddings should not be called for invalid input.");
    }

    private sealed class EmptyEmbeddings : IEmbeddingRepository
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>([]);
    }
}
