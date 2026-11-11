using AiInterview.Common.Configuration;
using AiInterview.Common.Enums;
using AiInterview.DAL.Models;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.DAL.Repositories.Vectors;
using AiInterview.BLL.Questions;
using AiInterview.BLL.Retrieval;
using Microsoft.Extensions.Options;

namespace AiInterview.Tests.Questions;

public sealed class QuestionGeneratorTests
{
    private const string ValidJson = """
        [{"content":"Razor Pages là gì?","referenceAnswer":"Một mô hình web theo trang.","keyPoints":["PageModel","routing theo thư mục"],"bloomLevel":"Understand","sourceChunkIds":["doc-a:0"]}]
        """;

    [Fact]
    public async Task Generator_Uses_Only_Requested_Documents_And_Passes_Chunk_Text_To_Llm()
    {
        var kit = new Kit();
        await kit.AddAsync("doc-a", "Razor Pages có PageModel và routing theo thư mục.");
        await kit.AddAsync("doc-b", "SQL Server lưu ngân hàng câu hỏi.");
        var llm = new CapturingLlm(ValidJson);
        var generator = kit.Generator(llm);

        var drafts = await generator.GenerateAsync(new GenerateQuestionsRequest(["doc-a"], 1, [BloomLevel.Understand]));

        var draft = Assert.Single(drafts);
        Assert.Equal("Razor Pages là gì?", draft.Content);
        Assert.Equal(BloomLevel.Understand, draft.BloomLevel);
        Assert.Equal(new[] { "doc-a:0" }, draft.SourceChunkIds);
        Assert.Contains("Razor Pages có PageModel", llm.LastPrompt);
        Assert.DoesNotContain("SQL Server", llm.LastPrompt);
        Assert.Contains("CHUNK_IDS_JSON:[\"doc-a:0\"]", llm.LastPrompt);
    }

    [Fact]
    public async Task Fake_Llm_Creates_Requested_Number_Of_Drafts_Within_Selected_Level()
    {
        var kit = new Kit();
        await kit.AddAsync("doc-a", "Razor Pages xử lý request bằng PageModel.");

        var drafts = await kit.Generator(new FakeLlmRepository())
            .GenerateAsync(new GenerateQuestionsRequest(["doc-a"], 4, [BloomLevel.Apply]));

        Assert.Equal(4, drafts.Count);
        Assert.All(drafts, q => Assert.Equal(BloomLevel.Apply, q.BloomLevel));
        Assert.All(drafts, q => Assert.Equal("doc-a:0", Assert.Single(q.SourceChunkIds)));
        Assert.Equal(4, drafts.Select(q => q.Content).Distinct().Count());
    }

    [Fact]
    public async Task Generator_Reports_When_Selected_Document_Has_No_Indexed_Chunks()
    {
        var kit = new Kit();
        await kit.AddAsync("doc-a", "Only the other document has vectors.");
        var llm = new CapturingLlm(ValidJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => kit.Generator(llm)
            .GenerateAsync(new GenerateQuestionsRequest(["missing"], 1)));
        Assert.Null(llm.LastPrompt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Generator_Rejects_Count_Outside_Allowed_Range(int count)
    {
        var kit = new Kit();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => kit.Generator(new CapturingLlm(ValidJson))
            .GenerateAsync(new GenerateQuestionsRequest(["doc-a"], count)));
    }

    [Fact]
    public async Task Generator_Rejects_Empty_Document_Selection()
    {
        var kit = new Kit();
        await Assert.ThrowsAsync<ArgumentException>(() => kit.Generator(new CapturingLlm(ValidJson))
            .GenerateAsync(new GenerateQuestionsRequest([], 1)));
    }

    [Fact]
    public void Parser_Rejects_Question_Count_Mismatch()
    {
        Assert.Throws<FormatException>(() => QuestionGeneratorService.Parse(ValidJson, new HashSet<string> { "doc-a:0" }, 2, null));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("[{}]")]
    [InlineData("not json")]
    public void Parser_Rejects_Nonconforming_Json(string json)
    {
        Assert.Throws<FormatException>(() => QuestionGeneratorService.Parse(json, new HashSet<string> { "doc-a:0" }, 1, null));
    }

    [Fact]
    public void Parser_Rejects_Bloom_Level_Outside_Request()
    {
        Assert.Throws<FormatException>(() => QuestionGeneratorService.Parse(
            ValidJson, new HashSet<string> { "doc-a:0" }, 1, [BloomLevel.Apply]));
    }

    [Fact]
    public void Parser_Trims_Text_And_Preserves_Source_Ids()
    {
        const string json = """
            [{"content":"  Question?  ","referenceAnswer":"  Answer  ","keyPoints":["Point"],"bloomLevel":"Remember","sourceChunkIds":["doc-a:0"]}]
            """;

        var draft = Assert.Single(QuestionGeneratorService.Parse(json, new HashSet<string> { "doc-a:0" }, 1, null));

        Assert.Equal("Question?", draft.Content);
        Assert.Equal("Answer", draft.ReferenceAnswer);
        Assert.Equal("doc-a:0", Assert.Single(draft.SourceChunkIds));
    }

    [Fact]
    public void Parser_Rejects_Duplicate_Questions_Ignoring_Case_And_Spaces()
    {
        const string json = """
            [
              {"content":"What is a PageModel?","referenceAnswer":"Answer","keyPoints":["Point"],"bloomLevel":"Understand","sourceChunkIds":["doc-a:0"]},
              {"content":"  what is a pagemodel?  ","referenceAnswer":"Answer","keyPoints":["Point"],"bloomLevel":"Understand","sourceChunkIds":["doc-a:0"]}
            ]
            """;

        var error = Assert.Throws<FormatException>(() => QuestionGeneratorService.Parse(
            json, new HashSet<string> { "doc-a:0" }, 2, null));

        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Kit
    {
        private readonly FakeEmbeddingRepository _embeddings = new(Options.Create(new GeminiOptions { EmbeddingDimensions = 64 }));
        private readonly InMemoryVectorRepository _vectors = new();

        public QuestionGeneratorService Generator(ILlmRepository llm)
            => new(new RetrievalService(_embeddings, _vectors), llm);

        public async Task AddAsync(string documentId, string text)
        {
            var vector = (await _embeddings.EmbedAsync([text]))[0];
            await _vectors.UpsertAsync([new VectorPoint($"{documentId}:0", vector, documentId, 0, 1, text)]);
        }
    }

    private sealed class CapturingLlm(string response) : ILlmRepository
    {
        public string? LastPrompt { get; private set; }

        public Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default)
        {
            Assert.Contains("QUESTION_GENERATION", systemInstruction);
            LastPrompt = userPrompt;
            return Task.FromResult(response);
        }
    }
}
