using GitHubEmailScraper.Infrastructure;
using GitHubEmailScraper.Models;
using GitHubEmailScraper.Pipeline;
using GitHubEmailScraper.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger (before DI is ready) ────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File(
        path: "output/logs/scraper-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // ── Configuration ─────────────────────────────────────────────────────────
    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddEnvironmentVariables("GHSCRAPER_"); // override with GHSCRAPER_GITHUB__PERSONALACCESSTOKEN etc.

    builder.Services.AddSerilog();

    // ── Bind AppSettings ──────────────────────────────────────────────────────
    var appSettings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();
    builder.Services.AddSingleton(appSettings);

    // ── Shared stats singleton ────────────────────────────────────────────────
    builder.Services.AddSingleton<PipelineStats>();

    // ── Infrastructure ────────────────────────────────────────────────────────
    builder.Services.AddSingleton<UserAgentRotator>();
    builder.Services.AddSingleton<ProxyRotator>();
    builder.Services.AddTransient<RateLimiterHandler>(); // Transient — one per HttpClient pipeline

    // ── HTTP clients ──────────────────────────────────────────────────────────
    // "GitHub": REST API client — resilience pipeline (outer) → rate-limiter handler (inner)
    builder.Services
        .AddHttpClient("GitHub", (sp, client) =>
        {
            var cfg = sp.GetRequiredService<AppSettings>().GitHub;
            client.BaseAddress = new Uri(cfg.BaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.DefaultRequestHeaders.Add("User-Agent", "GitHubEmailScraper/1.0");

            if (!string.IsNullOrWhiteSpace(cfg.PersonalAccessToken))
                client.DefaultRequestHeaders.Add(
                    "Authorization", $"Bearer {cfg.PersonalAccessToken}");
        })
        // Rate-limiter FIRST (outermost) — enforces search-API pacing and reads rate headers
        .AddHttpMessageHandler<RateLimiterHandler>()
        // Resilience SECOND (inner) — handles retries / timeouts per attempt
        .AddResilienceHandler("GitHubResilience", RetryPolicies.ConfigureGitHubResilience);

    // "Web": plain HTTPS client for scraping GitHub profile pages and personal websites
    builder.Services
        .AddHttpClient("Web", (sp, client) =>
        {
            var rotator = sp.GetRequiredService<UserAgentRotator>();
            client.DefaultRequestHeaders.Add("User-Agent", rotator.GetNext());
            client.DefaultRequestHeaders.Add(
                "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<ICheckpointService,     CheckpointService>();
    builder.Services.AddSingleton<IGitHubSearchService,   GitHubSearchService>();
    builder.Services.AddSingleton<IProfileParserService,  ProfileParserService>();
    builder.Services.AddSingleton<IEmailExtractionService, EmailExtractionService>();
    builder.Services.AddSingleton<ICsvWriterService,      CsvWriterService>();
    builder.Services.AddSingleton<ISqliteExportService,   SqliteExportService>();
    builder.Services.AddSingleton<IDashboardService,      DashboardService>();

    // ── Pipeline ──────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<SearchWorker>();
    builder.Services.AddSingleton<ProfileWorker>();
    builder.Services.AddSingleton<ExtractionWorker>();
    builder.Services.AddSingleton<PipelineOrchestrator>();

    // ── Build & run ───────────────────────────────────────────────────────────
    using var host = builder.Build();

    // Graceful shutdown on Ctrl+C
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true; // prevent abrupt termination
        Log.Information("Cancellation requested — finishing in-flight operations...");
        cts.Cancel();
    };

    var authenticated = !string.IsNullOrWhiteSpace(appSettings.GitHub.PersonalAccessToken);
    Log.Information(
        "GitHub Email Scraper starting. Output → {CsvPath}. Mode: {Mode}",
        appSettings.Output.CsvPath,
        authenticated ? "authenticated (30 search req/min)" : "unauthenticated (10 search req/min)");

    var orchestrator = host.Services.GetRequiredService<PipelineOrchestrator>();
    await orchestrator.RunAsync(cts.Token);

    Log.Information("Done. Check the output/ folder for results and logs.");
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
