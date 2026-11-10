using AiInterview.Common.Enums;
using AiInterview.Data;
using AiInterview.Data.Entities;
using AiInterview.Data.Repositories.Questions;
using AiInterview.Data.Repositories.Turns;
using AiInterview.Services.Interview;
using AiInterview.Services.Questions;
using AiInterview.Services.TurnLog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Tests.Questions;

/// <summary>
/// Mỗi lần gọi service chạy trong một scope riêng, đúng như một request HTTP thật
/// (DbContext không dùng chung giữa các request). Dùng chung một <see cref="AppDbContext"/>
/// cho nhiều lượt gọi sẽ khiến EF báo "another instance with the same key value is already being tracked".
/// </summary>
public sealed class QuestionFlowTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _provider = default!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Chạy một thao tác trên <see cref="IQuestionService"/> trong một scope riêng.</summary>
    private async Task<T> QuestionsAsync<T>(Func<IQuestionService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        var service = new QuestionService(new QuestionRepository(scope.ServiceProvider.GetRequiredService<AppDbContext>()));
        return await action(service);
    }

    private async Task QuestionsAsync(Func<IQuestionService, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        var service = new QuestionService(new QuestionRepository(scope.ServiceProvider.GetRequiredService<AppDbContext>()));
        await action(service);
    }

    /// <summary>Chạy một thao tác trên <see cref="ITurnLogService"/> trong một scope riêng.</summary>
    private async Task<T> TurnsAsync<T>(Func<ITurnLogService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        var service = new TurnLogService(new TurnRepository(scope.ServiceProvider.GetRequiredService<AppDbContext>()));
        return await action(service);
    }

    /// <summary>Ghi thẳng SQL trong một scope riêng (dữ liệu chuẩn bị cho test).</summary>
    private async Task SeedAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void Parser_Rejects_Invented_Chunk_Id()
    {
        const string json = "[{\"content\":\"Question\",\"referenceAnswer\":\"Answer\",\"keyPoints\":[\"Point\"],\"bloomLevel\":\"Understand\",\"sourceChunkIds\":[\"invented:0\"]}]";
        Assert.Throws<FormatException>(() => QuestionGeneratorService.Parse(json, new HashSet<string> { "real:0" }, 1, null));
    }

    [Fact]
    public async Task Draft_Can_Be_Edited_And_Approved_But_Not_Reviewed_Twice()
    {
        var draft = Assert.Single(await QuestionsAsync(s => s.SaveDraftsAsync(
            [new QuestionDraft("Question", "Answer", ["Point"], BloomLevel.Understand, ["doc:0"])], "doc")));
        await QuestionsAsync(s => s.UpdateAsync(new UpdateQuestionRequest(draft.Id, "Edited", "Better answer", ["First", "Second"], BloomLevel.Apply)));
        await QuestionsAsync(s => s.ApproveAsync(draft.Id));
        var approved = await QuestionsAsync(s => s.GetAsync(draft.Id));
        Assert.Equal(QuestionStatus.Approved, approved!.Status);
        Assert.Equal("Edited", approved.Content);
        Assert.Equal(new[] { "First", "Second" }, approved.KeyPoints);
        await Assert.ThrowsAsync<InvalidOperationException>(() => QuestionsAsync(s => s.RejectAsync(draft.Id)));
    }

    [Fact]
    public async Task Turn_Log_Save_Is_Idempotent_And_Can_Be_Listed_By_Session()
    {
        var session = new InterviewSession { Title = "Test", StudentName = "Student" };
        await SeedAsync(async db =>
        {
            db.InterviewSessions.Add(session);
            await db.SaveChangesAsync();
        });

        var now = DateTime.UtcNow;
        var turn = new TurnMessage("turn-1", session.Id, null, "Question", "Answer", false, false, null, ["doc:0"], now, now);
        Assert.True(await TurnsAsync(s => s.SaveAsync(turn)));
        Assert.False(await TurnsAsync(s => s.SaveAsync(turn)));
        var listed = Assert.Single(await TurnsAsync(s => s.ListBySessionAsync(session.Id)));
        Assert.Equal("turn-1", listed.TurnId);
        Assert.Equal("doc:0", Assert.Single(listed.Sources));
    }

    [Fact]
    public async Task Rejected_Question_Is_Excluded_From_Approved_Bank()
    {
        var drafts = await QuestionsAsync(s => s.SaveDraftsAsync(
            [new QuestionDraft("First", "Answer", ["Point"], BloomLevel.Remember, ["doc:0"]),
             new QuestionDraft("Second", "Answer", ["Point"], BloomLevel.Apply, ["doc:1"])], "doc"));

        await QuestionsAsync(s => s.ApproveAsync(drafts[0].Id));
        await QuestionsAsync(s => s.RejectAsync(drafts[1].Id));

        var approved = Assert.Single(await QuestionsAsync(s => s.ListAsync(QuestionStatus.Approved)));
        Assert.Equal(drafts[0].Id, approved.Id);
        var rejected = Assert.Single(await QuestionsAsync(s => s.ListAsync(QuestionStatus.Rejected)));
        Assert.Equal(drafts[1].Id, rejected.Id);
        Assert.Empty(await QuestionsAsync(s => s.ListAsync(QuestionStatus.Draft)));
    }

    [Fact]
    public async Task Invalid_Draft_Does_Not_Write_Any_Questions()
    {
        var drafts = new[]
        {
            new QuestionDraft("Valid", "Answer", ["Point"], BloomLevel.Apply, ["doc:0"]),
            new QuestionDraft("", "Answer", ["Point"], BloomLevel.Apply, ["doc:1"])
        };

        await Assert.ThrowsAsync<ArgumentException>(() => QuestionsAsync(s => s.SaveDraftsAsync(drafts, "doc")));

        Assert.Empty(await QuestionsAsync(s => s.ListAsync()));
    }

    [Fact]
    public async Task Approved_Question_Cannot_Be_Edited_Or_Rejected()
    {
        var draft = Assert.Single(await QuestionsAsync(s => s.SaveDraftsAsync(
            [new QuestionDraft("Question", "Answer", ["Point"], BloomLevel.Understand, ["doc:0"])], "doc")));
        await QuestionsAsync(s => s.ApproveAsync(draft.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => QuestionsAsync(s => s.UpdateAsync(
            new UpdateQuestionRequest(draft.Id, "Changed", "Changed", ["Changed"], BloomLevel.Apply))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => QuestionsAsync(s => s.RejectAsync(draft.Id)));

        var stored = await QuestionsAsync(s => s.GetAsync(draft.Id));
        Assert.Equal("Question", stored!.Content);
        Assert.Equal(QuestionStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task Delete_Removes_Question_And_Missing_Id_Is_Reported()
    {
        var draft = Assert.Single(await QuestionsAsync(s => s.SaveDraftsAsync(
            [new QuestionDraft("Question", "Answer", ["Point"], BloomLevel.Understand, ["doc:0"])], "doc")));

        await QuestionsAsync(s => s.DeleteAsync(draft.Id));

        Assert.Null(await QuestionsAsync(s => s.GetAsync(draft.Id)));
        Assert.Empty(await QuestionsAsync(s => s.ListAsync()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => QuestionsAsync(s => s.DeleteAsync(draft.Id)));
    }
}
