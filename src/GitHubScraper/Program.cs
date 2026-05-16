using System;
using Microsoft.EntityFrameworkCore;
using Serilog;
using GitHubScraper.Data;
using GitHubScraper.Models.Settings;
using GitHubScraper.Infrastructure.Http;
using Microsoft.Extensions.Http.Resilience;
using GitHubScraper.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// MVC
builder.Services.AddControllersWithViews();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Options
var githubOptions = builder.Configuration.GetSection("GitHub").Get<GitHubOptions>();
builder.Services.AddSingleton(githubOptions!);

// Utilities
builder.Services.AddSingleton<UserAgentRotator>();

// Named HTTP clients
builder.Services.AddHttpClient("GitHub", (sp, c) =>
{
    c.BaseAddress = new Uri(githubOptions.BaseUrl);
    c.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
})
.AddResilienceHandler("GitHubResilience", RetryPolicies.ConfigureGitHubResilience);

builder.Services.AddHttpClient("Web", (sp, c) =>
{
    c.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
});

// Application services
builder.Services.AddSingleton<OperationManager>();
builder.Services.AddTransient<DbWriterService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Operations}/{action=Index}/{id?}");

// Ensure database migrated + recover interrupted operations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed default operations if none exist
    await SeedData.SeedAsync(db);

    var manager = scope.ServiceProvider.GetRequiredService<OperationManager>();
    await manager.RecoverInterruptedOperationsAsync();
}

await app.RunAsync();
