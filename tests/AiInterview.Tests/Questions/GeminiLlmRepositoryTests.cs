using System.Net;
using System.Text;
using AiInterview.Common.Configuration;
using AiInterview.Data.Repositories.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiInterview.Tests.Questions;

public sealed class GeminiLlmRepositoryTests
{
    private const string ResponseJson = """
        {"candidates":[{"content":{"parts":[{"text":"[{\"content\":\"Question\"}]"}]}}]}
        """;

    [Fact]
    public async Task Generate_Sends_System_Instruction_User_Prompt_And_Json_Mode()
    {
        var handler = new SequenceHandler(() => Json(ResponseJson));
        var repository = Create(handler);

        var result = await repository.GenerateJsonAsync("You are a teacher", "Create one question");

        Assert.Equal("[{\"content\":\"Question\"}]", result);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("https://example.test/v1beta/models/test-model:generateContent", handler.LastUri);
        Assert.Contains("You are a teacher", handler.LastBody);
        Assert.Contains("Create one question", handler.LastBody);
        Assert.Contains("application/json", handler.LastBody);
    }

    [Fact]
    public async Task Generate_Retries_Rate_Limit_And_Then_Returns_Json()
    {
        var handler = new SequenceHandler(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero) }
            },
            () => Json(ResponseJson));

        var result = await Create(handler).GenerateJsonAsync("system", "prompt");

        Assert.Equal("[{\"content\":\"Question\"}]", result);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Generate_Retries_Server_Error()
    {
        var handler = new SequenceHandler(
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero) }
            },
            () => Json(ResponseJson));

        await Create(handler).GenerateJsonAsync("system", "prompt");

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Generate_Rejects_Empty_Model_Before_Sending_Request()
    {
        var handler = new SequenceHandler(() => Json(ResponseJson));
        var repository = Create(handler, model: "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GenerateJsonAsync("system", "prompt"));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Generate_Rejects_Empty_Gemini_Text()
    {
        var handler = new SequenceHandler(() => Json("""{"candidates":[{"content":{"parts":[{"text":""}]}}]}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).GenerateJsonAsync("system", "prompt"));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Generate_Does_Not_Retry_Client_Error()
    {
        var handler = new SequenceHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GenerateJsonAsync("system", "prompt"));

        Assert.Equal(1, handler.Calls);
    }

    private static GeminiLlmRepository Create(SequenceHandler handler, string model = "test-model")
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        return new GeminiLlmRepository(new SingleClientFactory(client),
            Options.Create(new GeminiOptions { Model = model }), NullLogger<GeminiLlmRepository>.Instance);
    }

    private static HttpResponseMessage Json(string content)
        => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("gemini", name);
            return client;
        }
    }

    private sealed class SequenceHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _next;
        public int Calls => _next;
        public string? LastUri { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri?.ToString();
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var index = Interlocked.Increment(ref _next) - 1;
            return responses[Math.Min(index, responses.Length - 1)]();
        }
    }
}
