using AiInterview.Common.Enums;
using AiInterview.BLL.Interview;

namespace AiInterview.Tests.Engine;

public class InterviewSessionTests
{
    [Fact]
    public async Task Create_Selects_Approved_Questions_And_Stores_Initial_State()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(5);

        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("Vấn đáp 1", "Nguyễn Văn A", QuestionCount: 3)));

        Assert.Equal(3, session.QuestionCount);
        Assert.Equal(SessionStatus.Created, session.Status);
        var state = await kit.States.GetAsync(session.Id.ToString());
        Assert.NotNull(state);
        Assert.Equal(3, state!.QuestionIds.Count);
        Assert.Equal(3, state.QuestionIds.Distinct().Count());
        Assert.Equal(InterviewState.AskQuestion, state.State);
        Assert.Equal(state.QuestionIds[0], state.QuestionId);
    }

    [Fact]
    public async Task Create_Uses_Configured_Defaults_When_Request_Leaves_Them_Empty()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);

        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("", "Trần Thị B")));

        Assert.Equal(kit.Options.QuestionCount, session.QuestionCount);
        Assert.Equal(kit.Options.MaxFollowUps, session.MaxFollowUps);
        Assert.Equal(kit.Options.AnswerSeconds, session.AnswerSeconds);
        Assert.Equal("Interview for Trần Thị B", session.Title);
    }

    [Fact]
    public async Task Create_Fails_Early_When_The_Approved_Bank_Is_Too_Small()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("x", "A", QuestionCount: 3))));

        Assert.Contains("only 2", ex.Message);
        Assert.Equal(0, await kit.CountSessionsAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_Rejects_Invalid_Question_Count(int count)
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("x", "A", QuestionCount: count))));
    }

    [Fact]
    public async Task Create_Requires_A_Student_Name()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);

        await Assert.ThrowsAsync<ArgumentException>(
            () => kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("x", "   ", QuestionCount: 1))));
    }

    [Fact]
    public async Task Consecutive_Sessions_Do_Not_Reuse_The_Same_Questions()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(6);

        // Thí sinh 1 thi hết 3 câu để các câu được đánh dấu đã dùng.
        var first = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S1", "Thí sinh 1", QuestionCount: 3)));
        var firstIds = (await kit.States.GetAsync(first.Id.ToString()))!.QuestionIds.ToList();
        await kit.RunAsync(s => s.StartAsync(first.Id));
        for (var i = 0; i < 3; i++)
        {
            var turnId = Guid.NewGuid().ToString("N");
            await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(first.Id, turnId, "Câu trả lời.")));
        }

        // Thí sinh 2 thi ngay sau đó: phải nhận tập câu khác.
        var second = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S2", "Thí sinh 2", QuestionCount: 3)));
        var secondIds = (await kit.States.GetAsync(second.Id.ToString()))!.QuestionIds;

        Assert.Equal(3, secondIds.Distinct().Count());
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    [Fact]
    public async Task Start_Sets_Deadline_And_Marks_The_Session_In_Progress()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2, AnswerSeconds: 45)));

        var view = await kit.RunAsync(s => s.StartAsync(session.Id));

        Assert.Equal(InterviewState.AskQuestion, view.State);
        Assert.Equal(0, view.QuestionIndex);
        Assert.Equal(2, view.QuestionCount);
        Assert.Equal(45, view.RemainingSeconds);
        Assert.False(view.IsFollowUp);
        Assert.False(string.IsNullOrWhiteSpace(view.CurrentQuestionText));

        var stored = await kit.GetSessionAsync(session.Id);
        Assert.Equal(SessionStatus.InProgress, stored!.Status);
        Assert.Equal(kit.Clock.UtcNow, stored.StartedAt);
    }

    [Fact]
    public async Task Start_Is_Idempotent_And_Keeps_The_Current_Question()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));

        var first = await kit.RunAsync(s => s.StartAsync(session.Id));
        kit.Clock.Advance(TimeSpan.FromSeconds(10));
        var second = await kit.RunAsync(s => s.StartAsync(session.Id));

        Assert.Equal(first.CurrentQuestionText, second.CurrentQuestionText);
        Assert.Equal(50, second.RemainingSeconds);
        Assert.Equal(1, await kit.CountSessionsAsync());
    }

    [Fact]
    public async Task Start_Rejects_An_Unknown_Session()
    {
        using var kit = new EngineTestKit();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => kit.RunAsync(s => s.StartAsync(Guid.NewGuid())));
    }

    [Fact]
    public async Task Start_Rejects_A_Completed_Session()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 1)));
        await kit.RunAsync(s => s.StartAsync(session.Id));
        await kit.RunAsync(s => s.FinishAsync(session.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => kit.RunAsync(s => s.StartAsync(session.Id)));
        Assert.Contains("Completed", ex.Message);
    }

    [Fact]
    public async Task Start_Recovers_When_The_State_Expired()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));

        // Trạng thái Redis hết TTL (ví dụ pod bị restart / quá 2 giờ).
        await kit.States.DeleteAsync(session.Id.ToString());

        var view = await kit.RunAsync(s => s.StartAsync(session.Id));

        Assert.Equal(InterviewState.AskQuestion, view.State);
        Assert.False(string.IsNullOrWhiteSpace(view.CurrentQuestionText));
    }

    [Fact]
    public async Task Finish_Marks_The_Session_Completed_And_Clears_The_State()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await kit.RunAsync(s => s.FinishAsync(session.Id));

        var stored = await kit.GetSessionAsync(session.Id);
        Assert.Equal(SessionStatus.Completed, stored!.Status);
        Assert.Equal(kit.Clock.UtcNow, stored.CompletedAt);
        Assert.Null(await kit.States.GetAsync(session.Id.ToString()));
    }

    [Fact]
    public async Task Finish_Is_Idempotent()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await kit.RunAsync(s => s.FinishAsync(session.Id));
        var firstCompletedAt = (await kit.GetSessionAsync(session.Id))!.CompletedAt;
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.RunAsync(s => s.FinishAsync(session.Id));

        Assert.Equal(firstCompletedAt, (await kit.GetSessionAsync(session.Id))!.CompletedAt);
    }

    [Fact]
    public async Task GetState_Returns_Null_For_Unknown_Session_Or_Expired_State()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));

        Assert.Null(await kit.RunAsync(s => s.GetStateAsync(Guid.NewGuid())));
        await kit.States.DeleteAsync(session.Id.ToString());
        Assert.Null(await kit.RunAsync(s => s.GetStateAsync(session.Id)));
    }
}
