using AiInterview.Common.Constants;
using AiInterview.DAL.Repositories.Queue;
using AiInterview.BLL.Interview;
using AiInterview.BLL.TurnLog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.TurnLog;

public sealed class TurnLogWorkerTests
{
    [Fact]
    public async Task Worker_Writes_Turn_And_Leaves_Dead_Letter_Empty()
    {
        var service = new RecordingTurnLogService();
        var queue = new InMemoryMessageQueueRepository();
        await queue.PublishAsync(QueueNames.TurnsStream, Turn("turn-1").ToFields());
        await using var provider = Provider(queue, service);
        var worker = Worker(provider, queue);

        await RunUntilAsync(worker, () => service.Saved.Count == 1);

        Assert.Equal("turn-1", Assert.Single(service.Saved).TurnId);
        Assert.Empty(queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)));
    }

    [Fact]
    public async Task Worker_Accepts_Duplicate_TurnId_Without_Dead_Letter()
    {
        var service = new RecordingTurnLogService();
        var queue = new InMemoryMessageQueueRepository();
        await queue.PublishAsync(QueueNames.TurnsStream, Turn("same-turn").ToFields());
        await queue.PublishAsync(QueueNames.TurnsStream, Turn("same-turn").ToFields());
        await using var provider = Provider(queue, service);
        var worker = Worker(provider, queue);

        await RunUntilAsync(worker, () => service.Calls == 2);

        Assert.Single(service.Saved);
        Assert.Equal(2, service.Calls);
        Assert.Empty(queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)));
    }

    [Fact]
    public async Task Worker_Dead_Letters_Invalid_Message_Without_Stopping_The_Loop()
    {
        var service = new RecordingTurnLogService();
        var queue = new InMemoryMessageQueueRepository();
        await queue.PublishAsync(QueueNames.TurnsStream, new Dictionary<string, string> { ["turnId"] = "malformed" });
        await queue.PublishAsync(QueueNames.TurnsStream, Turn("valid-after-error").ToFields());
        await using var provider = Provider(queue, service);
        var worker = Worker(provider, queue);

        await RunUntilAsync(worker, () => queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)).Count == 1
                                          && service.Saved.Count == 1);

        var dead = Assert.Single(queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)));
        Assert.Equal("malformed", dead.Fields["turnId"]);
        Assert.True(dead.Fields.ContainsKey("reason"));
        Assert.Equal("valid-after-error", Assert.Single(service.Saved).TurnId);
    }

    [Fact]
    public async Task Worker_Retries_Transient_Failure_Before_Saving()
    {
        var service = new RecordingTurnLogService { FailFirstCalls = 1 };
        var queue = new InMemoryMessageQueueRepository();
        await queue.PublishAsync(QueueNames.TurnsStream, Turn("retry-turn").ToFields());
        await using var provider = Provider(queue, service);
        var worker = Worker(provider, queue);

        await RunUntilAsync(worker, () => service.Saved.Count == 1);

        Assert.Equal(2, service.Calls);
        Assert.Empty(queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)));
    }

    [Fact]
    public async Task Worker_Dead_Letters_Persistent_Failure_After_Three_Attempts()
    {
        var service = new RecordingTurnLogService { FailFirstCalls = int.MaxValue };
        var queue = new InMemoryMessageQueueRepository();
        await queue.PublishAsync(QueueNames.TurnsStream, Turn("failing-turn").ToFields());
        await using var provider = Provider(queue, service);
        var worker = Worker(provider, queue);

        await RunUntilAsync(worker, () => queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)).Count == 1);

        Assert.Equal(3, service.Calls);
        Assert.Empty(service.Saved);
        var dead = Assert.Single(queue.Peek(QueueNames.DeadLetter(QueueNames.TurnsStream)));
        Assert.Equal("failing-turn", dead.Fields["turnId"]);
        Assert.Contains("Simulated SQL failure", dead.Fields["reason"]);
    }

    private static TurnMessage Turn(string id)
    {
        var now = DateTime.UtcNow;
        return new TurnMessage(id, Guid.NewGuid(), null, "Question", "Answer", false, false,
            "Answered", ["doc:0"], now, now.AddSeconds(5));
    }

    private static ServiceProvider Provider(InMemoryMessageQueueRepository queue, RecordingTurnLogService log)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageQueueRepository>(queue);
        services.AddScoped<ITurnLogService>(_ => log);
        return services.BuildServiceProvider();
    }

    private static TurnLogWorker Worker(ServiceProvider provider, IMessageQueueRepository queue)
        => new(provider.GetRequiredService<IServiceScopeFactory>(), queue, NullLogger<TurnLogWorker>.Instance);

    private static async Task RunUntilAsync(TurnLogWorker worker, Func<bool> done)
    {
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!done() && DateTime.UtcNow < deadline) await Task.Delay(20);
            Assert.True(done(), "Worker did not process the queued message before timeout.");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    private sealed class RecordingTurnLogService : ITurnLogService
    {
        private readonly HashSet<string> _ids = new();
        private readonly List<TurnMessage> _saved = new();
        private int _calls;

        public int FailFirstCalls { get; set; }
        public int Calls => Volatile.Read(ref _calls);
        public IReadOnlyList<TurnMessage> Saved { get { lock (_ids) return _saved.ToList(); } }

        public Task<bool> SaveAsync(TurnMessage turn, CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call <= FailFirstCalls) throw new InvalidOperationException("Simulated SQL failure");
            lock (_ids)
            {
                if (!_ids.Add(turn.TurnId)) return Task.FromResult(false);
                _saved.Add(turn);
            }
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<TurnDto>> ListBySessionAsync(Guid sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnDto>>([]);

        public Task<IReadOnlyList<TurnDto>> ListRecentAsync(int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnDto>>([]);
    }
}
