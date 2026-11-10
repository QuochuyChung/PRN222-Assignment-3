using AiInterview.Data.Repositories.Ai;
using AiInterview.Services.Interview;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.Services;

/// <summary>Lỗi khi gọi LLM (mạng, hạn mức, key sai, quá thời gian) không được làm hỏng lượt thi.</summary>
public class FollowUpDeciderResilienceTests
{
    private sealed class ThrowingLlm(Exception error) : ILlmRepository
    {
        public Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default)
            => Task.FromException<string>(error);
    }

    private static FollowUpDecider Create(Exception error)
        => new(new ThrowingLlm(error), NullLogger<FollowUpDecider>.Instance);

    private static FollowUpRequest Request() => new(
        "Razor Pages khác MVC ở điểm nào?",
        "Razor Pages tổ chức theo PageModel.",
        new[] { "PageModel" },
        "mỗi trang có một PageModel",
        0,
        2,
        Array.Empty<AiInterview.Services.Retrieval.RetrievedChunk>());

    [Fact]
    public async Task Http_Failure_Is_Treated_As_Answer_Is_Good_Enough()
    {
        var decision = await Create(new HttpRequestException("Gemini unreachable")).DecideAsync(Request());

        Assert.False(decision.NeedFollowUp);
        Assert.Null(decision.FollowUpQuestion);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public async Task Timeout_Is_Handled_Too()
    {
        // HttpClient báo quá thời gian bằng TaskCanceledException dù người dùng không hủy.
        var decision = await Create(new TaskCanceledException("timeout")).DecideAsync(Request());

        Assert.False(decision.NeedFollowUp);
    }

    [Fact]
    public async Task Real_Cancellation_By_The_Caller_Still_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Create(new OperationCanceledException(cts.Token)).DecideAsync(Request(), cts.Token));
    }
}
