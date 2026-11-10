using AiInterview.Common.Enums;
using AiInterview.Data.Repositories.Sessions;
using AiInterview.Data.Models;

namespace AiInterview.Tests.Engine;

public class SessionStateRepositoryTests
{
    [Fact]
    public async Task Save_Then_Get_Returns_The_Same_State()
    {
        var repo = new InMemorySessionStateRepository();
        var state = new SessionState
        {
            State = InterviewState.Listening,
            QuestionIndex = 2,
            QuestionId = Guid.NewGuid(),
            FollowUpCount = 1,
            Deadline = DateTime.UtcNow.AddSeconds(30),
            QuestionIds = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }
        };

        await repo.SaveAsync("s1", state);
        var loaded = await repo.GetAsync("s1");

        Assert.NotNull(loaded);
        Assert.Equal(InterviewState.Listening, loaded!.State);
        Assert.Equal(2, loaded.QuestionIndex);
        Assert.Equal(state.QuestionId, loaded.QuestionId);
        Assert.Equal(1, loaded.FollowUpCount);
        Assert.Equal(state.Deadline, loaded.Deadline);
        Assert.Equal(state.QuestionIds, loaded.QuestionIds);
    }

    [Fact]
    public async Task Get_Returns_A_Copy_So_Callers_Cannot_Mutate_Stored_State()
    {
        var repo = new InMemorySessionStateRepository();
        await repo.SaveAsync("s1", new SessionState { FollowUpCount = 0, QuestionIds = { Guid.NewGuid() } });

        var first = await repo.GetAsync("s1");
        first!.FollowUpCount = 99;
        first.QuestionIds.Clear();

        var second = await repo.GetAsync("s1");
        Assert.Equal(0, second!.FollowUpCount);
        Assert.Single(second.QuestionIds);
    }

    [Fact]
    public async Task Get_Returns_Null_For_Unknown_Or_Deleted_Session()
    {
        var repo = new InMemorySessionStateRepository();
        Assert.Null(await repo.GetAsync("missing"));

        await repo.SaveAsync("s1", new SessionState());
        await repo.DeleteAsync("s1");

        Assert.Null(await repo.GetAsync("s1"));
    }

    [Fact]
    public async Task TryLock_Is_Exclusive_Per_Session_And_Turn()
    {
        var repo = new InMemorySessionStateRepository();
        var ttl = TimeSpan.FromSeconds(5);

        Assert.True(await repo.TryLockAsync("s1", "turn-1", ttl));
        Assert.False(await repo.TryLockAsync("s1", "turn-1", ttl)); // cùng turn: chặn
        Assert.True(await repo.TryLockAsync("s1", "turn-2", ttl));  // turn khác: cho phép
        Assert.True(await repo.TryLockAsync("s2", "turn-1", ttl));  // phiên khác: cho phép
    }

    [Fact]
    public async Task TryLock_Is_Released_When_Ttl_Expires()
    {
        var repo = new InMemorySessionStateRepository();

        Assert.True(await repo.TryLockAsync("s1", "turn-1", TimeSpan.FromMilliseconds(30)));
        await Task.Delay(60);

        Assert.True(await repo.TryLockAsync("s1", "turn-1", TimeSpan.FromSeconds(5)));
    }
}
