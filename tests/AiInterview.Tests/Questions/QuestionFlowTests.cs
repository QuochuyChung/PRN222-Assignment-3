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

namespace AiInterview.Tests.Questions;

public sealed class QuestionFlowTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
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
        var service = new QuestionService(new QuestionRepository(_db));
        var draft = Assert.Single(await service.SaveDraftsAsync(
            [new QuestionDraft("Question", "Answer", ["Point"], BloomLevel.Understand, ["doc:0"])], "doc"));
        await service.UpdateAsync(new UpdateQuestionRequest(draft.Id, "Edited", "Better answer", ["First", "Second"], BloomLevel.Apply));
        await service.ApproveAsync(draft.Id);
        var approved = await service.GetAsync(draft.Id);
        Assert.Equal(QuestionStatus.Approved, approved!.Status);
        Assert.Equal("Edited", approved.Content);
        Assert.Equal(new[] { "First", "Second" }, approved.KeyPoints);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(draft.Id));
    }

    [Fact]
    public async Task Turn_Log_Save_Is_Idempotent_And_Can_Be_Listed_By_Session()
    {
        var session = new InterviewSession { Title = "Test", StudentName = "Student" };
        _db.InterviewSessions.Add(session);
        await _db.SaveChangesAsync();
        var service = new TurnLogService(new TurnRepository(_db));
        var now = DateTime.UtcNow;
        var turn = new TurnMessage("turn-1", session.Id, null, "Question", "Answer", false, false, null, ["doc:0"], now, now);
        Assert.True(await service.SaveAsync(turn));
        Assert.False(await service.SaveAsync(turn));
        var listed = Assert.Single(await service.ListBySessionAsync(session.Id));
        Assert.Equal("turn-1", listed.TurnId);
        Assert.Equal("doc:0", Assert.Single(listed.Sources));
    }

    [Fact]
    public async Task Rejected_Question_Is_Excluded_From_Approved_Bank()
    {
        var service = new QuestionService(new QuestionRepository(_db));
        var drafts = await service.SaveDraftsAsync(
            [new QuestionDraft("First", "Answer", ["Point"], BloomLevel.Remember, ["doc:0"]),
             new QuestionDraft("Second", "Answer", ["Point"], BloomLevel.Apply, ["doc:1"])], "doc");

        await service.ApproveAsync(drafts[0].Id);
        await service.RejectAsync(drafts[1].Id);

        var approved = Assert.Single(await service.ListAsync(QuestionStatus.Approved));
        Assert.Equal(drafts[0].Id, approved.Id);
        var rejected = Assert.Single(await service.ListAsync(QuestionStatus.Rejected));
        Assert.Equal(drafts[1].Id, rejected.Id);
        Assert.Empty(await service.ListAsync(QuestionStatus.Draft));
    }

    [Fact]
    public async Task Invalid_Draft_Does_Not_Write_Any_Questions()
    {
        var service = new QuestionService(new QuestionRepository(_db));
        var drafts = new[]
        {
            new QuestionDraft("Valid", "Answer", ["Point"], BloomLevel.Apply, ["doc:0"]),
            new QuestionDraft("", "Answer", ["Point"], BloomLevel.Apply, ["doc:1"])
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveDraftsAsync(drafts, "doc"));

        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task Approved_Question_Cannot_Be_Edited_Or_Rejected()
    {
        var service = new QuestionService(new QuestionRepository(_db));
        var draft = Assert.Single(await service.SaveDraftsAsync(
            [new QuestionDraft("Question", "Answer", ["Point"], BloomLevel.Understand, ["doc:0"])], "doc"));
        await service.ApproveAsync(draft.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            new UpdateQuestionRequest(draft.Id, "Changed", "Changed", ["Changed"], BloomLevel.Apply)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(draft.Id));

        var stored = await service.GetAsync(draft.Id);
        Assert.Equal("Question", stored!.Content);
        Assert.Equal(QuestionStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task Delete_Removes_Question_And_Missing_Id_Is_Reported()
    {
        var service = new QuestionService(new QuestionRepository(_db));
        var draft = Assert.Single(await service.SaveDraftsAsync(
            [new QuestionDraft("Question", "Answer", ["Point"], BloomLevel.Understand, ["doc:0"])], "doc"));

        await service.DeleteAsync(draft.Id);

        Assert.Null(await service.GetAsync(draft.Id));
        Assert.Empty(await service.ListAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(draft.Id));
    }
}
