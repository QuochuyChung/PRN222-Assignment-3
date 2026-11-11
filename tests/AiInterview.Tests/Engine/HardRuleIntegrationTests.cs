using AiInterview.Common.Enums;
using AiInterview.BLL.Interview;

namespace AiInterview.Tests.Engine;

public class HardRuleIntegrationTests
{
    [Fact]
    public async Task Follow_Up_Is_Asked_While_Turns_Remain()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        kit.Decider.Script = req => new FollowUpDecision(
            true, $"Hỏi xoáy #{req.FollowUpCount + 1}?", "Thiếu ý.", [], ["ý 1.b"], ["doc1:3"]);

        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, MaxFollowUps: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var first = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời.")));
        Assert.Equal(InterviewState.FollowUp, first.State.State);
        Assert.True(first.State.IsFollowUp);
        Assert.Equal(1, first.State.FollowUpCount);
        Assert.Equal("Hỏi xoáy #1?", first.State.CurrentQuestionText);

        var second = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t2", "Trả lời.")));
        Assert.Equal(InterviewState.FollowUp, second.State.State);
        Assert.Equal(2, second.State.FollowUpCount);

        // Lượt xoáy thứ 3: đã đạt MaxFollowUps ⇒ server ép chuyển câu dù LLM vẫn đòi hỏi xoáy.
        var third = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t3", "Trả lời.")));
        Assert.Equal(InterviewState.AskQuestion, third.State.State);
        Assert.False(third.State.IsFollowUp);
        Assert.Equal(0, third.State.FollowUpCount);
        Assert.Equal(1, third.State.QuestionIndex);
    }

    [Fact]
    public async Task Running_Out_Of_Time_Beats_The_Llm_And_Moves_To_The_Next_Question()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        kit.Decider.Script = _ => new FollowUpDecision(true, "Vẫn hỏi xoáy?", "Muốn hỏi tiếp.", [], [], []);

        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, AnswerSeconds: 10)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        // Client chậm: trả lời sau khi đã quá hạn.
        kit.Clock.Advance(TimeSpan.FromSeconds(30));
        var result = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "late", "Trả lời muộn.")));

        Assert.Equal(InterviewState.AskQuestion, result.State.State);
        Assert.Equal(1, result.State.QuestionIndex);
        Assert.Equal(0, result.State.FollowUpCount);
        Assert.Equal(10, result.State.RemainingSeconds);
    }

    [Fact]
    public async Task Remaining_Seconds_Counts_Down_From_The_Server_Deadline()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, AnswerSeconds: 60)));
        var started = await kit.RunAsync(s => s.StartAsync(session.Id));
        Assert.Equal(60, started.RemainingSeconds);

        kit.Clock.Advance(TimeSpan.FromSeconds(25));
        var state = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        Assert.Equal(35, state!.RemainingSeconds);
    }

    [Fact]
    public async Task Remaining_Seconds_Is_Zero_After_The_Deadline_And_Never_Negative()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, AnswerSeconds: 30)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        kit.Clock.Advance(TimeSpan.FromSeconds(31));
        var state = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        Assert.Equal(0, state!.RemainingSeconds);
    }

    [Fact]
    public async Task Silence_Prompts_Once_Without_Publishing_A_Turn()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var first = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "s1", "  ")));

        Assert.Null(first.Decision);
        Assert.Equal(0, kit.Decider.Calls);          // không gọi LLM khi im lặng
        Assert.Empty(kit.PublishedTurns);            // nhắc lại không phải một lượt hỏi/đáp
        Assert.Equal(0, first.State.QuestionIndex);  // vẫn ở câu cũ
        Assert.Equal(0, first.State.FollowUpCount);
    }

    [Fact]
    public async Task Silence_After_The_Prompt_Skips_The_Question_And_Publishes_A_Turn()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "s1", "")));
        var second = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "s2", "")));

        Assert.Equal(0, kit.Decider.Calls);
        Assert.Single(kit.PublishedTurns);
        Assert.Equal(1, second.State.QuestionIndex);  // đã bỏ qua sang câu 2
    }

    [Fact]
    public async Task Silence_Counter_Resets_On_The_Next_Question()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 3)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        // Câu 1: nhắc rồi bỏ qua.
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "a1", "")));
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "a2", "")));

        // Câu 2: phải được nhắc lại từ đầu, không bị bỏ qua ngay.
        var promptAgain = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "b1", "")));

        Assert.Equal(1, promptAgain.State.QuestionIndex);
        Assert.Single(kit.PublishedTurns);
    }

    [Fact]
    public async Task Submit_After_Finish_Is_Rejected()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 1)));
        await kit.RunAsync(s => s.StartAsync(session.Id));
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "only", "Trả lời.")));

        // Hết câu ⇒ phiên kết thúc và state bị xóa.
        var stored = await kit.GetSessionAsync(session.Id);
        Assert.Equal(SessionStatus.Completed, stored!.Status);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "after", "Trả lời."))));
    }

    [Fact]
    public async Task Completing_The_Last_Question_Finishes_The_Session()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời 1.")));
        var last = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t2", "Trả lời 2.")));

        Assert.Equal(InterviewState.Finished, last.State.State);
        Assert.Equal(0, last.State.RemainingSeconds);
        Assert.Equal(SessionStatus.Completed, (await kit.GetSessionAsync(session.Id))!.Status);
        Assert.Equal(2, kit.PublishedTurns.Count);
    }

    [Fact]
    public async Task Skip_Moves_To_The_Next_Question_And_Logs_The_Turn()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var view = await kit.RunAsync(s => s.SkipAsync(session.Id));

        Assert.Equal(1, view.QuestionIndex);
        Assert.Equal(InterviewState.AskQuestion, view.State);
        Assert.Equal(0, kit.Decider.Calls);
        var turn = Assert.Single(kit.PublishedTurns);
        Assert.Equal("", turn.Fields["transcript"]);
        Assert.Contains("bỏ qua", turn.Fields["reason"]);
    }

    [Fact]
    public async Task Skip_On_The_Last_Question_Finishes_The_Session()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 1)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var view = await kit.RunAsync(s => s.SkipAsync(session.Id));

        Assert.Equal(InterviewState.Finished, view.State);
        Assert.Equal(SessionStatus.Completed, (await kit.GetSessionAsync(session.Id))!.Status);
    }

    [Fact]
    public async Task The_Published_Turn_Matches_The_Question_Being_Asked()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        kit.Decider.Script = _ => new FollowUpDecision(true, "Câu xoáy?", "Thiếu ý.", [], [], ["doc1:3"]);

        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));
        var before = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Câu trả lời của tôi.")));

        var turn = Assert.Single(kit.PublishedTurns);
        Assert.Equal(before!.CurrentQuestionText, turn.Fields["question"]);
        Assert.Equal("False", turn.Fields["isFollowUp"]);
        Assert.Equal("True", turn.Fields["needFollowUp"]);
        Assert.Equal("Câu trả lời của tôi.", turn.Fields["transcript"]);
        Assert.Contains("doc1:3", turn.Fields["sources"]);
    }

    [Fact]
    public async Task Retrieval_Context_Is_Passed_To_The_Decider_When_Available()
    {
        using var kit = new EngineTestKit(withDecider: true, withRetrieval: true);
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời.")));

        Assert.Equal(1, kit.Retrieval.Calls);
        Assert.NotNull(kit.Decider.LastRequest);
        Assert.Single(kit.Decider.LastRequest!.ContextChunks);
        Assert.Equal("doc1:3", kit.Decider.LastRequest.ContextChunks[0].Id);
    }

    [Fact]
    public async Task Without_A_Decider_The_Engine_Still_Completes_The_Interview()
    {
        // Kiểm tra đường thoát an toàn: nếu IFollowUpDecider không resolve được (ví dụ lỗi cấu hình DI),
        // engine vẫn chạy hết phiên thay vì ném lỗi giữa lượt thi.
        using var kit = new EngineTestKit(withDecider: false);
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var first = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời.")));
        var second = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t2", "Trả lời.")));

        Assert.Equal(InterviewState.AskQuestion, first.State.State);
        Assert.Equal(InterviewState.Finished, second.State.State);
        Assert.Equal(2, kit.PublishedTurns.Count);
    }
}
