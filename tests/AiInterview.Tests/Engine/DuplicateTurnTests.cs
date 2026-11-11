using AiInterview.Common.Enums;
using AiInterview.BLL.Interview;

namespace AiInterview.Tests.Engine;

public class DuplicateTurnTests
{
    [Fact]
    public async Task Repeating_A_TurnId_Does_Not_Call_The_Llm_Twice()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        var turnId = Guid.NewGuid().ToString("N");
        var first = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, turnId, "Câu trả lời lần 1.")));
        var callsAfterFirst = kit.Decider.Calls;

        var second = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, turnId, "Câu trả lời lần 1.")));

        Assert.Equal(1, callsAfterFirst);
        Assert.Equal(callsAfterFirst, kit.Decider.Calls); // không gọi LLM lần hai
        Assert.Single(kit.PublishedTurns);                 // không đẩy thêm message
        Assert.Equal(first.State.QuestionIndex, second.State.QuestionIndex);
        Assert.Equal(first.State.FollowUpCount, second.State.FollowUpCount);
        Assert.Null(second.Decision);
    }

    [Fact]
    public async Task Different_Turn_Ids_Are_Processed_Independently()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "turn-1", "Trả lời 1.")));
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "turn-2", "Trả lời 2.")));

        Assert.Equal(2, kit.Decider.Calls);
        Assert.Equal(2, kit.PublishedTurns.Count);
    }

    [Fact]
    public async Task A_Replay_Returns_The_Current_State_Without_Rewinding()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(4);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 3)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        const string turnId = "same-turn";
        var first = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, turnId, "Trả lời.")));
        var replay = await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, turnId, "Trả lời.")));

        // Lượt đầu đã chuyển sang câu 2; replay phải trả đúng trạng thái hiện tại, không quay lại câu 1.
        Assert.Equal(1, first.State.QuestionIndex);
        Assert.Equal(1, replay.State.QuestionIndex);
        Assert.Equal(InterviewState.AskQuestion, replay.State.State);
    }

    [Fact]
    public async Task The_Lock_Is_Per_Session_So_The_Same_TurnId_Works_In_Another_Session()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(6);
        var a = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("A", "A", QuestionCount: 1)));
        var b = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("B", "B", QuestionCount: 1)));
        await kit.RunAsync(s => s.StartAsync(a.Id));
        await kit.RunAsync(s => s.StartAsync(b.Id));

        const string sharedTurnId = "turn-x";
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(a.Id, sharedTurnId, "Trả lời A.")));
        await kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(b.Id, sharedTurnId, "Trả lời B.")));

        Assert.Equal(2, kit.Decider.Calls);
    }

    [Fact]
    public async Task A_Blank_TurnId_Is_Rejected()
    {
        using var kit = new EngineTestKit();
        await kit.SeedApprovedAsync(3);
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("S", "A", QuestionCount: 2)));
        await kit.RunAsync(s => s.StartAsync(session.Id));

        await Assert.ThrowsAsync<ArgumentException>(
            () => kit.RunAsync(s => s.SubmitAnswerAsync(new SubmitAnswerRequest(session.Id, "  ", "Trả lời."))));
    }
}
