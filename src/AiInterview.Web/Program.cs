using System;
using AiInterview.Core.Interfaces;
using AiInterview.Infrastructure.DependencyInjection;
using AiInterview.Web.Hubs;
using AiInterview.Web.Services;
using AiInterview.Web.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Read Role from environment or configuration (web, worker, all)
string role = Environment.GetEnvironmentVariable("ROLE")
    ?? builder.Configuration.GetValue<string>("AiInterview:Role")
    ?? "all";

bool isWebRole = role.Equals("web", StringComparison.OrdinalIgnoreCase) || role.Equals("all", StringComparison.OrdinalIgnoreCase);
bool isWorkerRole = role.Equals("worker", StringComparison.OrdinalIgnoreCase) || role.Equals("all", StringComparison.OrdinalIgnoreCase);

// Register Core & Infrastructure services (Fake vs Real based on Infra:Mode)
builder.Services.AddAiInterviewInfrastructure(builder.Configuration);

if (isWebRole)
{
    builder.Services.AddRazorPages();
    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentProgressNotifier>();
}

if (isWorkerRole)
{
    builder.Services.AddHostedService<DocumentWorker>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

if (isWebRole)
{
    app.MapRazorPages();
    app.MapControllers();
    app.MapHub<DocumentHub>("/hubs/document");
}

if (isWorkerRole && !isWebRole)
{
    // Minimal health check endpoint for worker deployment
    app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", role = "worker" }));
}

app.Run();
