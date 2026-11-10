using System.Net;
using System.Text;
using System.Text.Json;
using AiInterview.Common.Configuration;
using AiInterview.Data.Registration;
using AiInterview.Data.Repositories.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Tests.Ingestion;

public class OllamaEmbeddingRepositoryTests
{
    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://ollama.test/") };
    }

    private sealed class StubHandler(Func<int, string[], HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public List<int> BatchSizes { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var inputs = doc.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()!).ToArray();
            BatchSizes.Add(inputs.Length);
            return respond(Calls, inputs);
        }
    }

    /// <summary>Mỗi text có dạng "t&lt;số&gt;"; vector trả về lặp lại đúng số đó, để kiểm tra thứ tự.</summary>
    private static HttpResponseMessage Vectors(string[] inputs, int dimensions)
    {
        var vectors = inputs.Select(t => Enumerable.Repeat(float.Parse(t[1..]), dimensions).ToArray());
        var json = JsonSerializer.Serialize(new { model = "nomic-embed-text", embeddings = vectors });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static OllamaEmbeddingRepository Create(HttpMessageHandler handler, int dimensions = 4, string model = "nomic-embed-text")
        => new(
            new StubFactory(handler),
            Options.Create(new OllamaOptions { EmbeddingModel = model }),
            Options.Create(new GeminiOptions { EmbeddingDimensions = dimensions }),
            NullLogger<OllamaEmbeddingRepository>.Instance);

    [Fact]
    public async Task Splits_Into_Batches_And_Keeps_Order()
    {
        var handler = new StubHandler((_, inputs) => Vectors(inputs, 4));
        var texts = Enumerable.Range(0, 70).Select(i => $"t{i}").ToList();

        var result = await Create(handler).EmbedAsync(texts);

        Assert.Equal(new[] { 32, 32, 6 }, handler.BatchSizes);
        Assert.Equal(70, result.Count);
        for (var i = 0; i < result.Count; i++) Assert.Equal(i, result[i][0]);
    }

    [Fact]
    public async Task Wrong_Dimension_Fails_With_A_Clear_Message()
    {
        var handler = new StubHandler((_, inputs) => Vectors(inputs, 8));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler, dimensions: 4).EmbedAsync(new[] { "t0" }));

        Assert.Contains("EmbeddingDimensions", ex.Message);
        Assert.Contains("8", ex.Message);
    }

    [Fact]
    public async Task Missing_Model_Tells_You_To_Pull_It()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).EmbedAsync(new[] { "t0" }));

        Assert.Contains("ollama pull nomic-embed-text", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Retries_When_Ollama_Is_Busy()
    {
        var handler = new StubHandler((call, inputs) => call == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Vectors(inputs, 4));

        var result = await Create(handler).EmbedAsync(new[] { "t3" });

        Assert.Equal(2, handler.Calls);
        Assert.Equal(3f, result[0][0]);
    }

    [Fact]
    public async Task Empty_Model_Name_Is_Rejected_Before_Calling_Ollama()
    {
        var handler = new StubHandler((_, inputs) => Vectors(inputs, 4));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler, model: " ").EmbedAsync(new[] { "t0" }));

        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(null, typeof(OllamaEmbeddingRepository))]
    [InlineData("Ollama", typeof(OllamaEmbeddingRepository))]
    [InlineData("Gemini", typeof(GeminiEmbeddingRepository))]
    [InlineData("gemini", typeof(GeminiEmbeddingRepository))]
    public void Provider_Setting_Chooses_The_Embedding_Repository_In_Real_Mode(string? provider, Type expected)
    {
        var values = new Dictionary<string, string?> { ["Embedding:Provider"] = provider };
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddData(config, new InfraOptions { Mode = InfraMode.Real });

        using var provider2 = services.BuildServiceProvider();

        Assert.IsType(expected, provider2.GetRequiredService<IEmbeddingRepository>());
    }

    [Fact]
    public void Fake_Mode_Still_Uses_The_Fake_Embedding()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddLogging();
        services.AddData(config, new InfraOptions { Mode = InfraMode.Fake });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeEmbeddingRepository>(provider.GetRequiredService<IEmbeddingRepository>());
    }
}
