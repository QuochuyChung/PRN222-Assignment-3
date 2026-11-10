using AiInterview.Common.Enums;
using AiInterview.Services.Interview;
using AiInterview.Web.Pages.Interview;
using SessionsModel = AiInterview.Web.Pages.Sessions.IndexModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xunit;

namespace AiInterview.Tests.Web;

public class InterviewRoomTests
{
    private readonly StubSessionService _sessionService = new();

    [Fact]
    public async Task Sessions_OnGetAsync_Populates_SessionList()
    {
        var model = new SessionsModel(_sessionService);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Single(model.Sessions);
        Assert.Equal("Session 1", model.Sessions[0].Title);
    }

    [Fact]
    public async Task Sessions_OnPostCreateAsync_Redirects_To_Room()
    {
        var model = new SessionsModel(_sessionService)
        {
            Form = new SessionsModel.CreateSessionForm
            {
                Title = "New Session",
                StudentName = "Sinh viên B",
                QuestionCount = 3,
                MaxFollowUps = 2,
                AnswerSeconds = 60
            }
        };

        var result = await model.OnPostCreateAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Interview/Room", redirect.PageName);
        Assert.NotNull(redirect.RouteValues?["sessionId"]);
    }

    [Fact]
    public async Task Room_OnGetAsync_Redirects_When_SessionId_Null()
    {
        var model = new RoomModel(_sessionService);

        var result = await model.OnGetAsync(null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Sessions/Index", redirect.PageName);
    }

    [Fact]
    public async Task Room_OnGetAsync_Loads_Session_And_State()
    {
        var sessionId = _sessionService.SingleSession.Id;
        var model = new RoomModel(_sessionService);

        var result = await model.OnGetAsync(sessionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Session);
        Assert.Equal("Session 1", model.Session.Title);
        Assert.NotNull(model.State);
        Assert.Equal("Câu hỏi mẫu 1?", model.State.CurrentQuestionText);
    }

    [Fact]
    public async Task Room_OnPostSubmitAnswerAsync_Returns_JsonResult()
    {
        var sessionId = _sessionService.SingleSession.Id;
        var request = new SubmitAnswerRequest(sessionId, "turn-123", "Câu trả lời mẫu.");
        var model = new RoomModel(_sessionService);

        var actionResult = await model.OnPostSubmitAnswerAsync(sessionId, request.TurnId, request.Transcript, request, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(actionResult);
        var submitResult = Assert.IsType<SubmitAnswerResult>(json.Value);
        Assert.NotNull(submitResult.State);
    }

    private sealed class StubSessionService : IInterviewSessionService
    {
        public SessionDto SingleSession { get; } = new(
            Guid.NewGuid(), "Session 1", "Sinh viên A", SessionStatus.Created, 3, 2, 60, DateTime.UtcNow, null, null);

        public Task<IReadOnlyList<SessionDto>> ListAsync(SessionStatus? status = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionDto>>(new List<SessionDto> { SingleSession });

        public Task<SessionDto?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<SessionDto?>(id == SingleSession.Id ? SingleSession : null);

        public Task<SessionDto> CreateAsync(CreateSessionRequest request, CancellationToken ct = default)
        {
            var created = new SessionDto(
                Guid.NewGuid(), request.Title, request.StudentName, SessionStatus.Created,
                request.QuestionCount ?? 3, request.MaxFollowUps ?? 2, request.AnswerSeconds ?? 60,
                DateTime.UtcNow, null, null);
            return Task.FromResult(created);
        }

        public Task<InterviewStateView> StartAsync(Guid sessionId, CancellationToken ct = default)
            => Task.FromResult(new InterviewStateView(sessionId, InterviewState.AskQuestion, 0, 3, "Câu hỏi mẫu 1?", false, 0, 2, 60));

        public Task<InterviewStateView?> GetStateAsync(Guid sessionId, CancellationToken ct = default)
            => Task.FromResult<InterviewStateView?>(new InterviewStateView(sessionId, InterviewState.AskQuestion, 0, 3, "Câu hỏi mẫu 1?", false, 0, 2, 60));

        public Task<SubmitAnswerResult> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct = default)
        {
            var state = new InterviewStateView(request.SessionId, InterviewState.AskQuestion, 1, 3, "Câu hỏi 2?", false, 0, 2, 60);
            return Task.FromResult(new SubmitAnswerResult(state, null));
        }

        public Task<InterviewStateView> SkipAsync(Guid sessionId, CancellationToken ct = default)
            => Task.FromResult(new InterviewStateView(sessionId, InterviewState.AskQuestion, 1, 3, "Câu hỏi 2?", false, 0, 2, 60));

        public Task FinishAsync(Guid sessionId, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
