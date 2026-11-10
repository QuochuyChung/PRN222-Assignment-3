using AiInterview.Services.Interview;

namespace AiInterview.Tests.Services;

public class TurnMessageTests
{
    private static TurnMessage Sample() => new(
        TurnId: "turn-0001",
        SessionId: Guid.NewGuid(),
        QuestionId: Guid.NewGuid(),
        Question: "Razor Pages khác MVC ở điểm nào?",
        Transcript: "Razor Pages thì mỗi trang có PageModel.",
        IsFollowUp: true,
        NeedFollowUp: false,
        Reason: "Đã đủ ý",
        Sources: new[] { "doc1:3", "doc1:4" },
        StartedAt: new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc),
        EndedAt: new DateTime(2026, 11, 9, 10, 0, 45, DateTimeKind.Utc));

    [Fact]
    public void Fields_Round_Trip_Preserves_Every_Value()
    {
        var original = Sample();

        var restored = TurnMessage.FromFields(original.ToFields());

        Assert.Equal(original.TurnId, restored.TurnId);
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Equal(original.QuestionId, restored.QuestionId);
        Assert.Equal(original.Question, restored.Question);
        Assert.Equal(original.Transcript, restored.Transcript);
        Assert.Equal(original.IsFollowUp, restored.IsFollowUp);
        Assert.Equal(original.NeedFollowUp, restored.NeedFollowUp);
        Assert.Equal(original.Reason, restored.Reason);
        Assert.Equal(original.Sources, restored.Sources);
        Assert.Equal(original.StartedAt, restored.StartedAt);
        Assert.Equal(original.EndedAt, restored.EndedAt);
    }

    [Fact]
    public void Empty_Optional_Fields_Become_Null_Or_Empty()
    {
        var original = Sample() with { QuestionId = null, Reason = null, Sources = Array.Empty<string>() };

        var restored = TurnMessage.FromFields(original.ToFields());

        Assert.Null(restored.QuestionId);
        Assert.Null(restored.Reason);
        Assert.Empty(restored.Sources);
    }

    [Fact]
    public void ToEntity_Maps_To_InterviewTurn()
    {
        var message = Sample();

        var entity = message.ToEntity();

        Assert.Equal(message.TurnId, entity.TurnId);
        Assert.Equal(message.SessionId, entity.SessionId);
        Assert.Equal(message.Question, entity.QuestionText);
        Assert.Equal(message.Sources, entity.Sources);
    }
}
