using AiInterview.DAL.Repositories.Ai;
using AiInterview.BLL.Interview;
using AiInterview.BLL.Retrieval;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.Engine;

public class FollowUpDeciderTests
{
    private static readonly string[] KeyPoints = ["PageModel", "routing theo thư mục", "phù hợp ứng dụng nhỏ"];

    private static FollowUpRequest Request(string transcript, int followUpCount = 0, int maxFollowUps = 2)
        => new(
            QuestionText: "Razor Pages khác MVC ở điểm nào?",
            ReferenceAnswer: "Razor Pages tổ chức theo từng trang với PageModel và routing theo thư mục; MVC tách Controller/View/Model.",
            KeyPoints: KeyPoints,
            Transcript: transcript,
            FollowUpCount: followUpCount,
            MaxFollowUps: maxFollowUps,
            ContextChunks:
            [
                new RetrievedChunk("doc1:3", "doc1", 3, "Razor Pages dùng PageModel cho từng trang.", 0.91f),
                new RetrievedChunk("doc1:4", "doc1", 4, "MVC phù hợp ứng dụng lớn, nhiều logic dùng chung.", 0.87f)
            ]);

    private static FollowUpDecider Decider(string rawJson, RecordingLlm? llm = null)
        => new(llm ?? new RecordingLlm(rawJson), NullLogger<FollowUpDecider>.Instance);

    [Fact]
    public async Task Full_Answer_Does_Not_Need_FollowUp()
    {
        var decider = Decider("""
            {"needFollowUp": false, "followUpQuestion": "", "reason": "Đã đủ cả ba ý chính.",
             "coveredKeyPoints": ["PageModel", "routing theo thư mục", "phù hợp ứng dụng nhỏ"],
             "missingKeyPoints": [], "sources": ["doc1:3"]}
            """);

        var decision = await decider.DecideAsync(Request("Razor Pages có PageModel theo từng thư mục, hợp với app nhỏ."));

        Assert.False(decision.NeedFollowUp);
        Assert.Null(decision.FollowUpQuestion);
        Assert.Empty(decision.MissingKeyPoints);
        Assert.Equal(3, decision.CoveredKeyPoints.Count);
        Assert.Equal(["doc1:3"], decision.Sources);
    }

    [Fact]
    public async Task Partial_Answer_Asks_A_Follow_Up_Question()
    {
        var decider = Decider("""
            {"needFollowUp": true, "followUpQuestion": "Khi nào bạn chọn Razor Pages thay vì MVC?",
             "reason": "Chưa nêu được tình huống chọn Razor Pages",
             "coveredKeyPoints": ["PageModel"], "missingKeyPoints": ["routing theo thư mục", "phù hợp ứng dụng nhỏ"],
             "sources": ["doc1:3"]}
            """);

        var decision = await decider.DecideAsync(Request("Razor Pages thì mỗi trang có một PageModel."));

        Assert.True(decision.NeedFollowUp);
        Assert.Equal("Khi nào bạn chọn Razor Pages thay vì MVC?", decision.FollowUpQuestion);
        Assert.Equal(2, decision.MissingKeyPoints.Count);
        Assert.Contains("tình huống", decision.Reason);
    }

    [Fact]
    public async Task Vague_Answer_Also_Asks_A_Follow_Up_Question()
    {
        var decider = Decider("""
            {"needFollowUp": true, "followUpQuestion": "Bạn nói rõ hơn PageModel xử lý gì trong một trang?",
             "reason": "Câu trả lời mơ hồ, không nêu ý chính nào",
             "coveredKeyPoints": [], "missingKeyPoints": ["PageModel", "routing theo thư mục", "phù hợp ứng dụng nhỏ"],
             "sources": []}
            """);

        var decision = await decider.DecideAsync(Request("Cái đó thì cũng na ná nhau thôi thầy."));

        Assert.True(decision.NeedFollowUp);
        Assert.Contains("PageModel", decision.FollowUpQuestion);
        Assert.Equal(3, decision.MissingKeyPoints.Count);
    }

    [Fact]
    public async Task Prompt_Contains_Question_Key_Points_Transcript_And_Context()
    {
        var llm = new RecordingLlm("""{"needFollowUp":false,"reason":"ok"}""");
        var decider = Decider("", llm);

        await decider.DecideAsync(Request("Em không chắc lắm.", followUpCount: 1));

        Assert.Contains("Razor Pages khác MVC ở điểm nào?", llm.UserPrompt);
        Assert.Contains("routing theo thư mục", llm.UserPrompt);
        Assert.Contains("Em không chắc lắm.", llm.UserPrompt);
        Assert.Contains("[doc1:3]", llm.UserPrompt);
        Assert.Contains("trang 3", llm.UserPrompt);
        Assert.Contains("lượt hỏi xoáy thứ 2", llm.UserPrompt);
        Assert.Contains("JSON", llm.SystemInstruction);
    }

    [Fact]
    public async Task Empty_Transcript_Skips_The_Llm_Entirely()
    {
        var llm = new RecordingLlm("""{"needFollowUp":true,"followUpQuestion":"Hỏi lại?"}""");
        var decider = Decider("", llm);

        var decision = await decider.DecideAsync(Request("   "));

        Assert.False(decision.NeedFollowUp);
        Assert.Equal(0, llm.Calls);
        Assert.Contains("rỗng", decision.Reason);
    }

    [Fact]
    public async Task Markdown_Fenced_Json_Is_Parsed()
    {
        var decider = Decider("""
            ```json
            {"needFollowUp": true, "followUpQuestion": "Ví dụ cụ thể?", "reason": "Thiếu ví dụ",
             "coveredKeyPoints": [], "missingKeyPoints": ["ví dụ"], "sources": []}
            ```
            """);

        var decision = await decider.DecideAsync(Request("Em nghĩ là nó khác."));

        Assert.True(decision.NeedFollowUp);
        Assert.Equal("Ví dụ cụ thể?", decision.FollowUpQuestion);
    }

    [Fact]
    public async Task Invalid_Json_Does_Not_Throw_And_Treats_The_Answer_As_Complete()
    {
        var decider = Decider("Xin lỗi, tôi không trả về JSON.");

        var decision = await decider.DecideAsync(Request("Razor Pages có PageModel."));

        Assert.False(decision.NeedFollowUp);
        Assert.Null(decision.FollowUpQuestion);
        Assert.Contains("không hợp lệ", decision.Reason);
    }

    [Fact]
    public async Task NeedFollowUp_Without_A_Question_Is_Downgraded_To_False()
    {
        var decider = Decider("""{"needFollowUp":true,"followUpQuestion":"","reason":"thiếu ý"}""");

        var decision = await decider.DecideAsync(Request("Em không nhớ."));

        Assert.False(decision.NeedFollowUp);
        Assert.Null(decision.FollowUpQuestion);
    }

    [Fact]
    public async Task Missing_Fields_Fall_Back_To_Empty_Collections()
    {
        var decider = Decider("""{"needFollowUp":false}""");

        var decision = await decider.DecideAsync(Request("Đủ ý rồi."));

        Assert.False(decision.NeedFollowUp);
        Assert.Empty(decision.CoveredKeyPoints);
        Assert.Empty(decision.MissingKeyPoints);
        Assert.Empty(decision.Sources);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public async Task Boolean_And_Array_Values_As_Strings_Are_Tolerated()
    {
        var decider = Decider("""{"needFollowUp":"true","followUpQuestion":"Giải thích thêm?","reason":"ok"}""");

        var decision = await decider.DecideAsync(Request("Em không chắc."));

        Assert.True(decision.NeedFollowUp);
    }

    private sealed class RecordingLlm(string response) : ILlmRepository
    {
        public int Calls { get; private set; }

        public string SystemInstruction { get; private set; } = "";

        public string UserPrompt { get; private set; } = "";

        public Task<string> GenerateJsonAsync(string systemInstruction, string userPrompt, CancellationToken ct = default)
        {
            Calls++;
            SystemInstruction = systemInstruction;
            UserPrompt = userPrompt;
            return Task.FromResult(response);
        }
    }
}
