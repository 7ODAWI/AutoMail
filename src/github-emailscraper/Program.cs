using GitHubEmailScraper.Components;
// Data layer removed: no direct EF Core DbContext in this edition
using GitHubEmailScraper.Infrastructure;
using GitHubEmailScraper.Models;
using GitHubEmailScraper.Services;
// EF Core removed
using Serilog;
using Serilog.Events;

// ── Bootstrap logger ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "output/logs/scraper-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ── Configuration ─────────────────────────────────────────────────────────
    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddEnvironmentVariables("GHSCRAPER_");

    // ── AppSettings ───────────────────────────────────────────────────────────
    var appSettings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();
    builder.Services.AddSingleton(appSettings);

    // ── Infrastructure ────────────────────────────────────────────────────────
    builder.Services.AddSingleton<UserAgentRotator>();
    builder.Services.AddSingleton<ProxyRotator>();
    // PipelineStats required by RateLimiterHandler for request-rate tracking
    builder.Services.AddSingleton<PipelineStats>();
    builder.Services.AddTransient<RateLimiterHandler>();

    // ── HTTP clients ──────────────────────────────────────────────────────────
    builder.Services
        .AddHttpClient("GitHub", (sp, client) =>
        {
            var cfg = sp.GetRequiredService<AppSettings>().GitHub;
            client.BaseAddress = new Uri(cfg.BaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.DefaultRequestHeaders.Add("User-Agent", "GitHubEmailScraper/1.0");
            if (!string.IsNullOrWhiteSpace(cfg.PersonalAccessToken))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cfg.PersonalAccessToken}");
        })
        .AddHttpMessageHandler<RateLimiterHandler>()
        .AddResilienceHandler("GitHubResilience", RetryPolicies.ConfigureGitHubResilience);

    builder.Services
        .AddHttpClient("Web", (sp, client) =>
        {
            var rotator = sp.GetRequiredService<UserAgentRotator>();
            client.DefaultRequestHeaders.Add("User-Agent", rotator.GetNext());
            client.DefaultRequestHeaders.Add(
                "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

    // Database persistence removed; SQL Server/EF Core code cleaned up

    // ── Operation management ──────────────────────────────────────────────────
    builder.Services.AddSingleton<OperationManager>();

    // ── Blazor Server ─────────────────────────────────────────────────────────
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
        app.UseExceptionHandler("/Error");

    // Automatic migrations removed; schema should be managed externally if needed.

    app.UseStaticFiles();
    app.UseAntiforgery();

    // Minimal API: download operation CSV
    app.MapGet("/api/operations/{id:guid}/download", (Guid id, OperationManager manager) =>
    {
        var op = manager.Get(id);
        if (op is null || !File.Exists(op.CsvPath))
            return Results.NotFound();
        return Results.File(op.CsvPath, "text/csv", $"operation-{id:N}.csv");
    });

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("GitHub Email Scraper Web started → http://localhost:5000");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
