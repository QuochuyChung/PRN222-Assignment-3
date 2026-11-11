using AiInterview.Common.Enums;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.BLL.Interview;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.Engine;

/// <summary>
/// ENG-10 / ENG-04: 5 kịch bản trong <c>seed/engine/answers.md</c> phải tái hiện được ở chế độ Fake,
/// tức là đi qua <see cref="FakeLlmRepository"/> thật + <see cref="FollowUpDecider"/> thật,
/// không dùng stub. Nhờ vậy demo không cần Gemini mà vẫn thấy được nhánh hỏi xoáy.
/// </summary>
public class SeedScenarioTests
{
    private static readonly string[] KeyPoints = ["PageModel", "routing theo thư mục", "phù hợp với ứng dụng nhỏ"];

    private static readonly FollowUpDecider Decider =
        new(new FakeLlmRepository(), NullLogger<FollowUpDecider>.Instance);

    private static FollowUpRequest Request(string transcript, int followUpCount = 0)
        => new(
            QuestionText: "Razor Pages khác MVC ở điểm nào?",
            ReferenceAnswer: "Razor Pages tổ chức theo từng trang với PageModel và routing theo thư mục; MVC tách Controller/View/Model.",
            KeyPoints: KeyPoints,
            Transcript: transcript,
            FollowUpCount: followUpCount,
            MaxFollowUps: 2,
            ContextChunks: []);

    [Fact]
    public async Task Scenario_1_Full_Answer_Does_Not_Ask_A_Follow_Up()
    {
        const string transcript =
            "Razor Pages thì mỗi trang có một PageModel riêng, routing dựa theo thư mục nên không cần khai báo route. " +
            "Nó phù hợp với ứng dụng nhỏ, ít logic dùng chung.";

        var decision = await Decider.DecideAsync(Request(transcript));

        Assert.False(decision.NeedFollowUp);
        Assert.Null(decision.FollowUpQuestion);
        Assert.Empty(decision.MissingKeyPoints);
    }

    [Fact]
    public async Task Scenario_2_Partial_Answer_Asks_A_Follow_Up_And_Reports_Missing_Points()
    {
        const string transcript = "Razor Pages thì mỗi trang có một PageModel riêng thầy ạ.";

        var decision = await Decider.DecideAsync(Request(transcript));

        Assert.True(decision.NeedFollowUp);
        Assert.False(string.IsNullOrWhiteSpace(decision.FollowUpQuestion));
        Assert.Equal(["routing theo thư mục", "phù hợp với ứng dụng nhỏ"], decision.MissingKeyPoints);
    }

    [Fact]
    public async Task Scenario_3_Vague_Answer_Asks_A_Clarifying_Question()
    {
        const string transcript = "Cái đó thì cũng na ná nhau thôi thầy, em học rồi nhưng không nhớ rõ.";

        var decision = await Decider.DecideAsync(Request(transcript));

        Assert.True(decision.NeedFollowUp);
        Assert.Empty(decision.CoveredKeyPoints);
        Assert.Equal(KeyPoints.Length, decision.MissingKeyPoints.Count);
    }

    [Fact]
    public async Task Scenario_4_Silence_Skips_The_Llm_Entirely()
    {
        // Transcript rỗng: FollowUpDecider không được gọi LLM, luật cứng mới là bên chuyển câu.
        var decision = await Decider.DecideAsync(Request(""));

        Assert.False(decision.NeedFollowUp);
        Assert.Contains("rỗng", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_5_Timeout_Is_Not_The_Deciders_Job()
    {
        // «Quá giờ» do HardRules quyết định (ENG-05), không phải LLM: dù LLM muốn hỏi xoáy,
        // ResolveAfterLlm phải trả NextQuestion.
        var decision = await Decider.DecideAsync(Request("Razor Pages thì mỗi trang có một PageModel riêng."));
        Assert.True(decision.NeedFollowUp);

        var next = HardRules.ResolveAfterLlm(
            llmWantsFollowUp: decision.NeedFollowUp,
            followUpCount: 0,
            maxFollowUps: 2,
            timedOut: true,
            hasMoreQuestions: true);

        Assert.Equal(InterviewState.NextQuestion, next);
    }

    [Fact]
    public async Task Fake_Llm_Still_Generates_Questions_For_Qst02()
    {
        var raw = await new FakeLlmRepository().GenerateJsonAsync(
            "QUESTION_GENERATION. Trả về mảng JSON.",
            "Sinh 2 câu hỏi vấn đáp bằng tiếng Việt. Mức Bloom: Apply.\nCHUNK_IDS_JSON:[\"doc:0\"]\n[doc:0] Trang 1: Nội dung");

        using var json = System.Text.Json.JsonDocument.Parse(raw);
        Assert.Equal(2, json.RootElement.GetArrayLength());
        Assert.Equal("Apply", json.RootElement[0].GetProperty("bloomLevel").GetString());
    }
}
