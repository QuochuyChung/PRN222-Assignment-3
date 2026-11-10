using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiInterview.Common.Enums;
using AiInterview.Services.Interview;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Tests.Web;

/// <summary>
/// ENG-07: endpoint của phòng phỏng vấn chạy trên app thật ở chế độ Fake
/// (Razor page handler, không có controller nên Program.cs không phải đổi).
/// POST cần token chống CSRF như mọi form Razor Pages; test lấy token từ chính trang.
/// </summary>
public class InterviewRoomEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Regex TokenPattern = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

    private HttpClient Client => factory.CreateClient();

    private async Task<SessionDto> CreateSessionAsync(int questions = 2, int followUps = 2)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInterviewSessionService>();
        return await service.CreateAsync(new CreateSessionRequest("Vấn đáp test", "Sinh viên test",
            QuestionCount: questions, MaxFollowUps: followUps, AnswerSeconds: 60));
    }

    /// <summary>Lấy token antiforgery từ trang phòng thi rồi POST kèm header.</summary>
    private async Task<HttpResponseMessage> PostAsync(HttpClient client, string query)
    {
        var html = await client.GetStringAsync("/Interview/Room");
        var token = TokenPattern.Match(html).Groups[1].Value;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Interview/Room?{query}");
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    private async Task<T?> PostJsonAsync<T>(HttpClient client, string query)
    {
        var response = await PostAsync(client, query);
        Assert.True(response.IsSuccessStatusCode, $"POST {query} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<T>(Json);
    }

    [Fact]
    public async Task Start_Returns_The_Question_And_Remaining_Seconds()
    {
        var client = Client;
        var session = await CreateSessionAsync();

        var response = await PostAsync(client, $"handler=Start&sessionId={session.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<InterviewStateView>(Json);
        Assert.NotNull(state);
        Assert.Equal(session.Id, state!.SessionId);
        Assert.Equal(InterviewState.AskQuestion, state.State);
        // Đồng hồ thật: vài chục ms trôi qua nên làm tròn xuống còn 59.
        Assert.InRange(state.RemainingSeconds, 55, 60);
        Assert.False(string.IsNullOrWhiteSpace(state.CurrentQuestionText));
    }

    [Fact]
    public async Task State_Returns_The_Current_Question_For_F5()
    {
        var client = Client;
        var session = await CreateSessionAsync();
        var started = await PostJsonAsync<InterviewStateView>(client, $"handler=Start&sessionId={session.Id}");

        var response = await client.GetAsync($"/Interview/Room?handler=State&sessionId={session.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<InterviewStateView>(Json);
        Assert.Equal(started!.CurrentQuestionText, state!.CurrentQuestionText);
    }

    [Fact]
    public async Task State_Returns_404_For_A_Session_That_Is_Not_Running()
    {
        var response = await Client.GetAsync($"/Interview/Room?handler=State&sessionId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAnswer_Advances_And_Is_Idempotent_Per_TurnId()
    {
        var client = Client;
        var session = await CreateSessionAsync();
        await PostJsonAsync<InterviewStateView>(client, $"handler=Start&sessionId={session.Id}");

        var turnId = Guid.NewGuid().ToString("N");
        var first = await PostJsonAsync<SubmitAnswerResult>(client,
            $"handler=SubmitAnswer&sessionId={session.Id}&turnId={turnId}&transcript=PageModel%20theo%20tung%20trang");

        Assert.NotNull(first);
        Assert.Equal(1, first!.State.QuestionIndex);

        // Gửi lại cùng turnId: không tiến thêm câu.
        var replay = await PostJsonAsync<SubmitAnswerResult>(client,
            $"handler=SubmitAnswer&sessionId={session.Id}&turnId={turnId}&transcript=PageModel%20theo%20tung%20trang");

        Assert.Equal(first.State.QuestionIndex, replay!.State.QuestionIndex);
    }

    [Fact]
    public async Task SubmitAnswer_Generates_A_TurnId_When_The_Client_Omits_One()
    {
        var client = Client;
        var session = await CreateSessionAsync();
        await PostJsonAsync<InterviewStateView>(client, $"handler=Start&sessionId={session.Id}");

        var response = await PostAsync(client, $"handler=SubmitAnswer&sessionId={session.Id}&transcript=Tra%20loi");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAnswer_Is_Rejected_When_The_Session_Was_Never_Started()
    {
        var client = Client;
        var session = await CreateSessionAsync();

        var response = await PostAsync(client, $"handler=SubmitAnswer&sessionId={session.Id}&turnId=t1&transcript=x");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Skip_Moves_To_The_Next_Question()
    {
        var client = Client;
        var session = await CreateSessionAsync();
        await PostJsonAsync<InterviewStateView>(client, $"handler=Start&sessionId={session.Id}");

        var view = await PostJsonAsync<InterviewStateView>(client, $"handler=Skip&sessionId={session.Id}");

        Assert.Equal(1, view!.QuestionIndex);
    }

    [Fact]
    public async Task Finish_Completes_The_Session_And_Later_State_Is_404()
    {
        var client = Client;
        var session = await CreateSessionAsync();
        await PostJsonAsync<InterviewStateView>(client, $"handler=Start&sessionId={session.Id}");

        var finish = await PostAsync(client, $"handler=Finish&sessionId={session.Id}");
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);

        var state = await client.GetAsync($"/Interview/Room?handler=State&sessionId={session.Id}");
        Assert.Equal(HttpStatusCode.NotFound, state.StatusCode);
    }

    [Fact]
    public async Task Unknown_Session_Returns_404_And_A_Completed_One_Returns_409()
    {
        var client = Client;

        var unknown = await PostAsync(client, $"handler=Start&sessionId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var session = await CreateSessionAsync();
        await PostJsonAsync<InterviewStateView>(client, $"handler=Start&sessionId={session.Id}");
        await PostAsync(client, $"handler=Finish&sessionId={session.Id}");

        var completed = await PostAsync(client, $"handler=Start&sessionId={session.Id}");
        Assert.Equal(HttpStatusCode.Conflict, completed.StatusCode);
    }

    [Fact]
    public async Task The_Room_Page_Renders_With_An_AntiForgery_Token()
    {
        var html = await Client.GetStringAsync("/Interview/Room");

        Assert.Contains("Interview room", html);
        Assert.Matches(TokenPattern, html);
    }

    [Fact]
    public async Task Posting_Without_The_Token_Is_Refused()
    {
        var client = Client;
        var session = await CreateSessionAsync();

        var response = await client.PostAsync($"/Interview/Room?handler=Start&sessionId={session.Id}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
