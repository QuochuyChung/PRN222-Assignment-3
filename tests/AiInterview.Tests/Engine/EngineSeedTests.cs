using AiInterview.Common.Enums;
using AiInterview.Data.Repositories.Questions;
using AiInterview.Data.Seeding;
using AiInterview.Services.Interview;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Tests.Engine;

/// <summary>ENG-10: seed của engine phải đủ câu <c>Approved</c> để tạo phiên nhiều câu ở chế độ Fake.</summary>
public class EngineSeedTests
{
    [Fact]
    public async Task EngineDataSeeder_Provides_Enough_Approved_Questions_For_A_Session()
    {
        using var kit = new EngineTestKit();

        // Seeder của BASE và của ENG chạy cùng nhau, đúng như DatabaseInitializer ở chế độ Fake.
        await kit.QueryAsync(db => new SampleDataSeeder().SeedAsync(db));
        await kit.QueryAsync(db => new EngineDataSeeder().SeedAsync(db));

        var approvedCount = await kit.QueryAsync(db => db.Questions.CountAsync(q => q.Status == QuestionStatus.Approved));
        Assert.True(approvedCount >= 3, $"Cần ít nhất 3 câu Approved, hiện có {approvedCount}.");

        // Và phiên 3 câu phải tạo được ngay.
        var session = await kit.RunAsync(s => s.CreateAsync(new CreateSessionRequest("Seed", "Sinh viên", QuestionCount: 3)));
        Assert.Equal(3, session.QuestionCount);
    }

    [Fact]
    public async Task EngineDataSeeder_Is_Idempotent()
    {
        using var kit = new EngineTestKit();

        await kit.QueryAsync(db => new EngineDataSeeder().SeedAsync(db));
        var first = await kit.QueryAsync(db => db.Questions.CountAsync());
        await kit.QueryAsync(db => new EngineDataSeeder().SeedAsync(db));
        var second = await kit.QueryAsync(db => db.Questions.CountAsync());

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task EngineDataSeeder_Does_Not_Duplicate_Questions_Already_Seeded_By_Base()
    {
        using var kit = new EngineTestKit();

        await kit.QueryAsync(db => new SampleDataSeeder().SeedAsync(db));
        var afterBase = await kit.QueryAsync(db => db.Questions.CountAsync());

        await kit.QueryAsync(db => new EngineDataSeeder().SeedAsync(db));
        var afterEngine = await kit.QueryAsync(db => db.Questions.CountAsync());

        // Câu "Razor Pages khác MVC ở điểm nào?" của BASE không được thêm lần hai.
        Assert.Equal(afterBase + 5, afterEngine);
    }

    [Fact]
    public void EngineDataSeeder_Runs_After_The_Base_Seeder()
        => Assert.True(new EngineDataSeeder().Order > new SampleDataSeeder().Order);

    [Fact]
    public async Task Every_Seeded_Question_Has_A_Reference_Answer_And_Key_Points()
    {
        using var kit = new EngineTestKit();
        await kit.QueryAsync(db => new EngineDataSeeder().SeedAsync(db));

        var questions = await kit.QueryAsync(db => new QuestionRepository(db).ListAsync(null, default));

        Assert.All(questions, q =>
        {
            Assert.False(string.IsNullOrWhiteSpace(q.Content));
            Assert.False(string.IsNullOrWhiteSpace(q.ReferenceAnswer));
            Assert.NotEmpty(q.KeyPoints);
        });
    }
}
