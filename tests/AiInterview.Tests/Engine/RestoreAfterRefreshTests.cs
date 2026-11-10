using AiInterview.Common.Enums;
using AiInterview.Services.Interview;

namespace AiInterview.Tests.Engine;

/// <summary>
/// ENG-09: sau khi F5, client chỉ gọi <c>state</c> và phải nhận đúng câu đang hỏi,
/// số lượt xoáy đã dùng và thời gian còn lại — không bị tua lại về câu 1.
/// </summary>
public class RestoreAfterRefreshTests
{
    [Fact]
    public async Task Refresh_Mid_Question_Restores_The_Same_Question_And_Countdown()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, AnswerSeconds: 60)));
        var started = await kit.RunAsync(s => s.StartAsync(session.Id));

        kit.Clock.Advance(TimeSpan.FromSeconds(18));
        var restored = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        Assert.Equal(started.CurrentQuestionText, restored!.CurrentQuestionText);
        Assert.Equal(started.QuestionIndex, restored.QuestionIndex);
        Assert.Equal(42, restored.RemainingSeconds);
        Assert.Equal(InterviewState.AskQuestion, restored.State);
    }

    [Fact]
    public async Task Refresh_During_A_Follow_Up_Restores_The_Follow_Up_Count_And_Text()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        kit.Decider.Script = req => new FollowUpDecision(
            true, $"Câu xoáy #{req.FollowUpCount + 1}?", "Thiếu ý.", [], ["ý 1.b"], []);

        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, MaxFollowUps: 2, AnswerSeconds: 45)));
        await kit.RunAsync(s => s.StartAsync(session.Id));
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời.")));

        kit.Clock.Advance(TimeSpan.FromSeconds(5));
        var restored = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        Assert.Equal(InterviewState.FollowUp, restored!.State);
        Assert.True(restored.IsFollowUp);
        Assert.Equal(1, restored.FollowUpCount);
        Assert.Equal(2, restored.MaxFollowUps);
        Assert.Equal("Câu xoáy #1?", restored.CurrentQuestionText);
        Assert.Equal(40, restored.RemainingSeconds);
    }

    [Fact]
    public async Task Refresh_After_Moving_To_The_Next_Question_Does_Not_Show_The_Old_One()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 3)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var first = await kit.RunAsync(s => s.GetStateAsync(session.Id));
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời.")));
        var second = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        Assert.Equal(1, second!.QuestionIndex);
        Assert.NotEqual(first!.CurrentQuestionText, second.CurrentQuestionText);
        Assert.Equal(0, second.FollowUpCount);
        Assert.False(second.IsFollowUp);
    }

    [Fact]
    public async Task Refresh_Does_Not_Extend_The_Deadline()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, AnswerSeconds: 30)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        kit.Clock.Advance(TimeSpan.FromSeconds(20));

        // F5 gọi state nhiều lần (SignalR hỏng thì client polling) — không được cộng thêm thời gian.
        Assert.Equal(10, (await kit.RunAsync(s => s.GetStateAsync(session.Id)))!.RemainingSeconds);
        Assert.Equal(10, (await kit.RunAsync(s => s.GetStateAsync(session.Id)))!.RemainingSeconds);
        Assert.Equal(10, (await kit.RunAsync(s => s.GetStateAsync(session.Id)))!.RemainingSeconds);
    }

    [Fact]
    public async Task Refresh_Does_Not_Consume_A_Follow_Up_Turn()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, MaxFollowUps: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        for (var i = 0; i < 5; i++)
        {
            await kit.RunAsync(s => s.GetStateAsync(session.Id));
        }

        Assert.Equal(0, kit.Decider.Calls);
        Assert.Empty(kit.PublishedTurns);
        Assert.Equal(0, (await kit.RunAsync(s => s.GetStateAsync(session.Id)))!.FollowUpCount);
    }

    [Fact]
    public async Task Refresh_After_The_Interview_Finished_Returns_The_Terminal_State()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 1)));
        await kit.RunAsync(s => s.StartAsync(session.Id));
        var finished = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "t1", "Trả lời.")));

        Assert.Equal(InterviewState.Finished, finished.State.State);

        // State trong Redis đã xoá ⇒ không còn gì để khôi phục, client hiển thị màn hình kết thúc.
        Assert.Null(await kit.RunAsync(s => s.GetStateAsync(session.Id)));
        Assert.Equal(SessionStatus.Completed, (await kit.GetSessionAsync(session.Id))!.Status);
    }

    [Fact]
    public async Task Refresh_Of_A_Session_That_Was_Never_Started_Still_Returns_The_First_Question()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));

        var state = await kit.RunAsync(s => s.GetStateAsync(session.Id));

        Assert.NotNull(state);
        Assert.Equal(InterviewState.AskQuestion, state!.State);
        Assert.Equal(0, state.RemainingSeconds); // chưa bắt đầu ⇒ chưa có deadline
    }
}
