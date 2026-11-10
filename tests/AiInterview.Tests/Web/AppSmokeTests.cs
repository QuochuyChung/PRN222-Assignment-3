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

    [Theory]
    [InlineData("/Questions")]
    [InlineData("/Sessions")]
    [InlineData("/Logs")]
    public async Task Placeholder_Pages_Render(string url)
    {
        var response = await _client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Not built yet", html);
    }
}
