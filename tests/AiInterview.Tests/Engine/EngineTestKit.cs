using AiInterview.Common.Configuration;
using AiInterview.Common.Constants;
using AiInterview.Common.Enums;
using AiInterview.Data;
using AiInterview.Data.Entities;
using AiInterview.Data.Models;
using AiInterview.Data.Repositories.Questions;
using AiInterview.Data.Repositories.Queue;
using AiInterview.Data.Repositories.Sessions;
using AiInterview.Data.Repositories.Turns;
using AiInterview.Services.Interview;
using AiInterview.Services.Retrieval;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Tests.Engine;

/// <summary>Đồng hồ điều khiển được: test hết giờ mà không phải chờ thật.</summary>
internal sealed class TestClock(DateTime start) : TimeProvider
{
    public DateTime UtcNow { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>
/// Ráp Interview Engine giống hệt DI thật: mỗi lần gọi service là một scope riêng (như một request),
/// SQLite in-memory cho SQL, in-memory cho state và hàng đợi.
/// Thiếu <c>ILlmRepository</c> (QST-02) nên dùng <see cref="StubDecider"/> / <see cref="StubRetrieval"/>.
/// </summary>
internal sealed class EngineTestKit : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public InMemorySessionStateRepository States { get; } = new();

    public InMemoryQuestionUsageRepository Usage { get; } = new();

    public InMemoryMessageQueueRepository Queue { get; } = new();

    public TestClock Clock { get; } = new(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));

    public StubDecider Decider { get; } = new();

    public StubRetrieval Retrieval { get; } = new();

    public InterviewOptions Options { get; } = new() { QuestionCount = 3, MaxFollowUps = 2, AnswerSeconds = 60 };

    public EngineTestKit(bool withDecider = true, bool withRetrieval = false)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<ITurnRepository, TurnRepository>();
        services.AddSingleton<ISessionStateRepository>(States);
        services.AddSingleton<IQuestionUsageRepository>(Usage);
        services.AddSingleton<IMessageQueueRepository>(Queue);
        services.AddSingleton<ITurnPublisher, TurnPublisher>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IInterviewSessionService, InterviewSessionService>();

        if (withDecider) services.AddSingleton<IFollowUpDecider>(Decider);
        if (withRetrieval) services.AddSingleton<IRetrievalService>(Retrieval);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    /// <summary>Chạy một thao tác trong một scope riêng, giống một request HTTP.</summary>
    public async Task<T> RunAsync<T>(Func<IInterviewSessionService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IInterviewSessionService>());
    }

    public async Task RunAsync(Func<IInterviewSessionService, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IInterviewSessionService>());
    }

    /// <summary>Đọc thẳng SQL bằng một scope riêng (không dùng chung change tracker với service).</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Chạy một thao tác ghi (ví dụ seeder) trong một scope riêng.</summary>
    public async Task QueryAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<InterviewSession?> GetSessionAsync(Guid id)
        => QueryAsync(db => db.InterviewSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id));

    public Task<int> CountSessionsAsync() => QueryAsync(db => db.InterviewSessions.CountAsync());

    public Task<int> CountTurnsAsync() => QueryAsync(db => db.InterviewTurns.CountAsync());

    /// <summary>Thêm câu hỏi <c>Approved</c> và trả về id theo đúng thứ tự đã thêm.</summary>
    public async Task<List<Guid>> SeedApprovedAsync(int count)
    {
        var list = Enumerable.Range(1, count).Select(i => new Question
        {
            Content = $"Câu hỏi {i}?",
            ReferenceAnswer = $"Đáp án mẫu {i}.",
            KeyPoints = [$"ý {i}.a", $"ý {i}.b"],
            BloomLevel = BloomLevel.Understand,
            Status = QuestionStatus.Approved
        }).ToList();

        await QueryAsync(async db =>
        {
            db.Questions.AddRange(list);
            return await db.SaveChangesAsync();
        });

        return list.Select(q => q.Id).ToList();
    }

    public IReadOnlyList<QueueMessage> PublishedTurns => Queue.Peek(QueueNames.TurnsStream);

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}

/// <summary>IFollowUpDecider giả: trả kịch bản đã hẹn trước, ghi lại số lần gọi.</summary>
internal sealed class StubDecider : IFollowUpDecider
{
    public int Calls { get; private set; }

    public FollowUpRequest? LastRequest { get; private set; }

    /// <summary>Kịch bản trả về; mặc định là "đủ ý, không hỏi xoáy".</summary>
    public Func<FollowUpRequest, FollowUpDecision> Script { get; set; } =
        _ => new FollowUpDecision(false, null, "Đã đủ ý.", [], [], []);

    public Task<FollowUpDecision> DecideAsync(FollowUpRequest request, CancellationToken ct = default)
    {
        Calls++;
        LastRequest = request;
        return Task.FromResult(Script(request));
    }
}

/// <summary>IRetrievalService giả cho prompt ngữ cảnh.</summary>
internal sealed class StubRetrieval : IRetrievalService
{
    public int Calls { get; private set; }

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string query, RetrievalScope scope, int topK, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<RetrievedChunk>>(
        [
            new RetrievedChunk("doc1:3", "doc1", 3, "Đoạn tài liệu liên quan.", 0.9f)
        ]);
    }
}
