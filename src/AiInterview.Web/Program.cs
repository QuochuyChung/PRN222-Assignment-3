using System.Text.Encodings.Web;
using System.Text.Unicode;
using AiInterview.Common.Configuration;
using AiInterview.Data;
using AiInterview.Data.Registration;
using AiInterview.Data.Seeding;
using AiInterview.Services.Registration;
using AiInterview.Web.Hubs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Infra:Mode (Fake|Real) và ROLE (web|worker|all) quyết định app chạy thế nào.
var infra = InfraOptions.From(builder.Configuration);
builder.Services.AddSingleton(infra);

// Kiến trúc 3 lớp: PageModel -> Service -> Repository.
builder.Services.AddData(builder.Configuration, infra);   // lớp Repository (Fake/Real)
builder.Services.AddServices(infra);                       // lớp Service

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("sql", tags: new[] { "ready" });

if (infra.RunsWeb)
{
    builder.Services.AddRazorPages();

    // Mặc định Razor mã hóa ký tự tiếng Việt thành &#x...; trong HTML. Cho phép mọi ký tự Unicode để HTML giữ nguyên chữ.
    builder.Services.AddWebEncoders(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

    var signalR = builder.Services.AddSignalR();
    var redis = builder.Configuration["Redis:ConnectionString"];
    if (infra.IsReal && !string.IsNullOrWhiteSpace(redis))
    {
        // Nhiều pod web cần backplane để SignalR gửi đúng client.
        signalR.AddStackExchangeRedis(redis);
    }
}

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(infra, app.Configuration);

if (infra.RunsWeb)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.MapRazorPages();
    app.MapHubs();
}

// Probe cho Kubernetes: live = tiến trình còn sống, ready = kết nối được SQL.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Cho phép test tích hợp (WebApplicationFactory<Program>).
public partial class Program;
