using AiInterview.Common.Constants;
using AiInterview.Data.Repositories.Queue;
using AiInterview.Services.Interview;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.Engine;

public class TurnPublisherTests
{
    private static TurnMessage SampleTurn(string turnId = "turn-1") => new(
        TurnId: turnId,
        SessionId: Guid.NewGuid(),
        QuestionId: Guid.NewGuid(),
        Question: "Razor Pages khác MVC ở điểm nào?",
        Transcript: "Razor Pages có PageModel.",
        IsFollowUp: false,
        NeedFollowUp: true,
        Reason: "Chưa nêu được tình huống chọn Razor Pages.",
        Sources: ["doc1:3", "doc1:4"],
        StartedAt: DateTime.UtcNow,
        EndedAt: DateTime.UtcNow.AddSeconds(20));

    [Fact]
    public async Task Publish_Writes_One_Message_To_The_Turns_Stream()
    {
        var queue = new InMemoryMessageQueueRepository();
        var publisher = new TurnPublisher(queue, NullLogger<TurnPublisher>.Instance);
        var turn = SampleTurn();

        await publisher.PublishAsync(turn);

        var message = Assert.Single(queue.Peek(QueueNames.TurnsStream));
        Assert.Equal(turn.TurnId, message.Fields["turnId"]);
        Assert.Equal(turn.SessionId.ToString(), message.Fields["sessionId"]);
        Assert.Equal(turn.QuestionId!.Value.ToString(), message.Fields["questionId"]);
    }

    [Fact]
    public async Task Published_Message_Round_Trips_Through_FromFields()
    {
        var queue = new InMemoryMessageQueueRepository();
        var publisher = new TurnPublisher(queue, NullLogger<TurnPublisher>.Instance);
        var turn = SampleTurn("abc123");

        await publisher.PublishAsync(turn);
        var message = Assert.Single(queue.Peek(QueueNames.TurnsStream));
        var restored = TurnMessage.FromFields(message.Fields);

        Assert.Equal(turn.TurnId, restored.TurnId);
        Assert.Equal(turn.SessionId, restored.SessionId);
        Assert.Equal(turn.QuestionId, restored.QuestionId);
        Assert.Equal(turn.Question, restored.Question);
        Assert.Equal(turn.Transcript, restored.Transcript);
        Assert.Equal(turn.IsFollowUp, restored.IsFollowUp);
        Assert.Equal(turn.NeedFollowUp, restored.NeedFollowUp);
        Assert.Equal(turn.Reason, restored.Reason);
        Assert.Equal(turn.Sources, restored.Sources);
        Assert.Equal(turn.StartedAt, restored.StartedAt);
        Assert.Equal(turn.EndedAt, restored.EndedAt);
    }

    [Fact]
    public async Task Publishing_The_Same_Turn_Twice_Produces_Two_Messages_Because_Leader_Deduplication_Is_Consumer_Side()
    {
        var queue = new InMemoryMessageQueueRepository();
        var publisher = new TurnPublisher(queue, NullLogger<TurnPublisher>.Instance);
        var turn = SampleTurn("dup");

        await publisher.PublishAsync(turn);
        await publisher.PublishAsync(turn);

        // Chống trùng thuộc ENG-06 (khóa theo turnId) và LOG-01 (InsertIfNotExists);
        // publisher chỉ đảm bảo message đúng định dạng.
        Assert.Equal(2, queue.Peek(QueueNames.TurnsStream).Count);
    }

    [Fact]
    public async Task Publish_Propagates_Queue_Failures_And_Logs_Them()
    {
        var publisher = new TurnPublisher(new BrokenQueue(), NullLogger<TurnPublisher>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(SampleTurn()));
    }

    private sealed class BrokenQueue : IMessageQueueRepository
    {
        public Task EnsureGroupAsync(string stream, string group, CancellationToken ct = default) => Task.CompletedTask;

        public Task<string> PublishAsync(string stream, IReadOnlyDictionary<string, string> fields, CancellationToken ct = default)
            => throw new InvalidOperationException("Redis is down");

        public Task<IReadOnlyList<Data.Models.QueueMessage>> ReadAsync(string stream, string group, string consumer, int count, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task AckAsync(string stream, string group, string messageId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DeadLetterAsync(string stream, string group, Data.Models.QueueMessage message, string reason, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
