using System.Text.Encodings.Web;
using System.Text.Unicode;
using AiInterview.Common.Configuration;
using AiInterview.Services.Registration;
using AiInterview.Web.Hubs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using StackExchange.Redis;

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

    // Data Protection keys sign the antiforgery tokens and cookies. By default they live inside the pod, so every restart or
    // deploy invalidates pages that are already open ("The antiforgery token could not be decrypted") and several web pods
    // cannot read each other's tokens. In Real mode keep one shared key ring in Redis.
    var dataProtection = builder.Services.AddDataProtection().SetApplicationName("ai-interview");
    var keyRingRedis = builder.Configuration["Redis:ConnectionString"];
    if (infra.IsReal && !string.IsNullOrWhiteSpace(keyRingRedis))
    {
        var redisOptions = ConfigurationOptions.Parse(keyRingRedis);
        redisOptions.AbortOnConnectFail = false; // keep retrying instead of failing the first request if Redis is briefly down
        var keyRingConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(redisOptions));
        dataProtection.PersistKeysToStackExchangeRedis(() => keyRingConnection.Value.GetDatabase(), "ai-interview:data-protection-keys");
    }
}

// SignalR is needed by BOTH roles. The web role hosts the hub (MapHubs below). The worker role only publishes
// progress: it never maps the hub, but through the Redis backplane its messages reach the browsers connected to the web pods.
// Without this the worker keeps the no-op notifier and the Documents page never hears that processing finished.
if (infra.RunsWeb || infra.RunsWorker)
{
    var signalR = builder.Services.AddSignalR();
    builder.Services.AddHubServices();
    var redis = builder.Configuration["Redis:ConnectionString"];
    if (infra.IsReal && !string.IsNullOrWhiteSpace(redis))
    {
        // Several pods (web and worker) share one backplane so SignalR reaches the right client.
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
