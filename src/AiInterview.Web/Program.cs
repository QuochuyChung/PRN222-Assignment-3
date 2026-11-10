using System.Text.Encodings.Web;
using System.Text.Unicode;
using AiInterview.Common.Configuration;
using AiInterview.Services.Registration;
using AiInterview.Web.Hubs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Infra:Mode (Fake|Real) and ROLE (web|worker|all) decide how the app runs.
var infra = InfraOptions.From(builder.Configuration);
builder.Services.AddSingleton(infra);

// 3-layer architecture: PageModel -> Service -> Repository.
// Web only knows the Service layer: AddServices registers the Repository layer internally.
builder.Services.AddServices(builder.Configuration, infra);

if (infra.RunsWeb)
{
    builder.Services.AddRazorPages();

    // By default Razor encodes non-ASCII characters (e.g. Vietnamese sample data) as &#x...;. Allow all Unicode ranges.
    builder.Services.AddWebEncoders(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

    var signalR = builder.Services.AddSignalR();
    builder.Services.AddHubServices();
    var redis = builder.Configuration["Redis:ConnectionString"];
    if (infra.IsReal && !string.IsNullOrWhiteSpace(redis))
    {
        // Several web pods need a backplane so SignalR reaches the right client.
        signalR.AddStackExchangeRedis(redis);
    }
}

var app = builder.Build();

await app.Services.InitializeApplicationAsync(infra, app.Configuration);

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

// Kubernetes probes: live = process is up, ready = SQL is reachable (check registered in the Data layer).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Allows integration tests (WebApplicationFactory<Program>).
public partial class Program;
