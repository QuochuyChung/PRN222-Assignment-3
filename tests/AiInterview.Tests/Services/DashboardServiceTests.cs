using AiInterview.DAL;
using AiInterview.DAL.Entities;
using AiInterview.Common.Enums;
using AiInterview.DAL.Models;
using AiInterview.DAL.Repositories.Documents;
using AiInterview.DAL.Repositories.Questions;
using AiInterview.DAL.Repositories.Sessions;
using AiInterview.DAL.Repositories.Turns;
using AiInterview.DAL.Seeding;
using AiInterview.BLL.Dashboard;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.Services;

/// <summary>Kiểm thử Service với Repository thật trên SQLite in-memory (đúng cách chạy chế độ Fake).</summary>
public sealed class DashboardServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await new SampleDataSeeder().SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private DashboardService CreateService(IDocumentRepository documents) => new(
        documents,
        new QuestionRepository(_db),
        new SessionRepository(_db),
        new TurnRepository(_db),
        NullLogger<DashboardService>.Instance);

    [Fact]
    public async Task Summary_Counts_Match_Sample_Data()
    {
        var documents = new FakeDocumentRepository();
        await documents.CreateAsync(new DocumentRecord { FileName = "a.pdf", Status = DocumentStatus.Success });
        await documents.CreateAsync(new DocumentRecord { FileName = "b.pdf", Status = DocumentStatus.Processing });

        var summary = await CreateService(documents).GetSummaryAsync();

        Assert.Equal(2, summary.TotalDocuments);
        Assert.Equal(1, summary.ProcessedDocuments);
        Assert.Equal(1, summary.ApprovedQuestions);
        Assert.Equal(2, summary.DraftQuestions);
        Assert.Equal(1, summary.TotalSessions);
        Assert.Equal(1, summary.CompletedSessions);
        Assert.Equal(2, summary.TotalTurns);
        Assert.Equal(2, summary.RecentTurns.Count);
    }

    [Fact]
    public async Task Recent_Turns_Are_Newest_First()
    {
        var summary = await CreateService(new FakeDocumentRepository()).GetSummaryAsync();

        Assert.True(summary.RecentTurns[0].EndedAt >= summary.RecentTurns[1].EndedAt);
        Assert.True(summary.RecentTurns[0].IsFollowUp);
    }

    [Fact]
    public async Task Turn_Insert_Is_Idempotent_By_TurnId()
    {
        var repo = new TurnRepository(_db);
        var session = (await new SessionRepository(_db).ListAsync()).First();
        var turn = new InterviewTurn { TurnId = "dup-1", SessionId = session.Id, QuestionText = "Câu hỏi" };

        var first = await repo.InsertIfNotExistsAsync(turn);
        var second = await repo.InsertIfNotExistsAsync(new InterviewTurn { TurnId = "dup-1", SessionId = session.Id, QuestionText = "Câu hỏi" });

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(3, await repo.CountAsync());
    }
}
