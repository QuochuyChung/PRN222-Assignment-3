using AiInterview.BLL.Documents;
using AiInterview.Web.Hubs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Tests.Web;

/// <summary>
/// Chạy hai pod riêng (web và worker) thì tiến độ do worker tạo ra phải đi qua SignalR mới tới được trình duyệt.
/// Worker không được dùng bản notifier "không làm gì".
/// </summary>
public class WorkerRoleTests
{
    [Theory]
    [InlineData("worker")]
    [InlineData("web")]
    [InlineData("all")]
    public void Every_Role_Pushes_Progress_Through_SignalR(string role)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("ROLE", role));

        var notifier = factory.Services.GetRequiredService<IDocumentProgressNotifier>();

        Assert.IsType<SignalRDocumentProgressNotifier>(notifier);
    }
}
