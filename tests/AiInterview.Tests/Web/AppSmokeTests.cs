using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiInterview.Tests.Web;

/// <summary>Chạy cả app ở chế độ Fake (mặc định trong appsettings.json) và gọi các trang chính.</summary>
public class AppSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_Endpoints_Return_Ok(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Renders_Sample_Data_Through_The_Three_Layers()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dashboard", html);
        Assert.Contains("Razor Pages khác MVC ở điểm nào?", html); // từ SampleDataSeeder, đi qua Service -> Repository
    }

    [Fact]
    public async Task Documents_Page_And_Status_Endpoint_Work()
    {
        var page = await _client.GetStringAsync("/Documents");
        var status = await _client.GetAsync("/Documents?handler=Status");

        Assert.Contains("Upload a file", page);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal("application/json", status.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Questions_Page_Renders_Real_Content()
    {
        var response = await _client.GetAsync("/Questions");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Question bank", html);
    }

    [Fact]
    public async Task Logs_Page_Renders_Real_Content()
    {
        var response = await _client.GetAsync("/Logs");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_Page_Renders_Successfully()
    {
        var response = await _client.GetAsync("/Sessions");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Phiên phỏng vấn", html);
        Assert.Contains("Kiểm tra Mic", html);
    }

    [Fact]
    public async Task Room_Page_Redirects_To_Sessions_When_No_SessionId()
    {
        var nonRedirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await nonRedirectClient.GetAsync("/Interview/Room");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Sessions", response.Headers.Location?.OriginalString);
    }
}
