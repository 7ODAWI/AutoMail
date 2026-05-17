using System.Collections.Concurrent;
using System.Text.Json;
using GitHubScraper.Data;
using GitHubScraper.Infrastructure.Http;
using GitHubScraper.Models.Entities;
using GitHubScraper.Models.Enums;
using GitHubScraper.Models.Settings;
using GitHubScraper.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace GitHubScraper.Services;

/// <summary>
/// Singleton that creates and manages independent scraping operations.
/// Each operation gets its own isolated pipeline service instances so filters,
/// checkpoints, and DB writes never cross-contaminate.
/// Operations are persisted to SQL Server; in-memory state tracks live execution.
/// </summary>
public sealed class OperationManager
{
    private readonly ConcurrentDictionary<Guid, RunningOperation> _running = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly GitHubOptions _githubOptions;
    private readonly ILogger<OperationManager> _logger;

    public OperationManager(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        ILoggerFactory loggerFactory,
        GitHubOptions githubOptions,
        ILogger<OperationManager> logger)
    {
        _scopeFactory  = scopeFactory;
        _httpFactory   = httpFactory;
        _loggerFactory = loggerFactory;
        _githubOptions = githubOptions;
        _logger        = logger;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<ScrapingOperation>> GetAllAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Operations
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<ScrapingOperation?> GetAsync(Guid id)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Operations.FindAsync(id);
    }

    /// <summary>Returns live PipelineStats if the operation is running, otherwise null.</summary>
    public PipelineStats? GetLiveStats(Guid id) =>
        _running.TryGetValue(id, out var r) ? r.Stats : null;

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<ScrapingOperation> CreateAsync(
        string name,
        List<string> keywords,
        List<string> locations,
        int minFollowers,
        string? githubToken)
    {
        var op = new ScrapingOperation
        {
            Name          = name,
            GitHubToken   = githubToken,
            KeywordsJson  = JsonSerializer.Serialize(keywords),
            LocationsJson = JsonSerializer.Serialize(locations),
            MinFollowers  = minFollowers,
            Status        = OperationStatus.Idle
        };

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Operations.Add(op);
        await db.SaveChangesAsync();

        return op;
    }

    public async Task StartAsync(Guid id)
    {
        if (_running.ContainsKey(id)) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var op = await db.Operations.FindAsync(id);
        if (op is null || op.Status == OperationStatus.Running) return;

        op.Status          = OperationStatus.Running;
        op.StartedAt       = DateTime.UtcNow;
        op.CompletedAt     = null;
        op.ProfilesScanned = 0;
        op.EmailsFound     = 0;
        op.Failures        = 0;
        op.CurrentUser     = null;
        op.CurrentQuery    = null;
        await db.SaveChangesAsync();

        var running = new RunningOperation();
        _running[id] = running;

        running.PipelineTask = Task.Run(() => RunOperationAsync(op, running));
    }

    public async Task StopAsync(Guid id)
    {
        if (_running.TryRemove(id, out var running))
        {
            running.Cts.Cancel();
            if (running.PipelineTask is not null)
            {
                try { await running.PipelineTask; } catch { /* already cancelled */ }
            }
            running.Cts.Dispose();
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var op = await db.Operations.FindAsync(id);
        if (op is not null && op.Status == OperationStatus.Running)
        {
            op.Status      = OperationStatus.Cancelled;
            op.CompletedAt = DateTime.UtcNow;
            op.CurrentUser  = null;
            op.CurrentQuery = null;
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await StopAsync(id);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var op = await db.Operations.FindAsync(id);
        if (op is not null)
        {
            db.Operations.Remove(op);
            await db.SaveChangesAsync();
        }
    }

    // ── Execution ──────────────────────────────────────────────────────────────

    private async Task RunOperationAsync(ScrapingOperation op, RunningOperation running)
    {
        var ct = running.Cts.Token;
        var stats = running.Stats;

        // Load checkpoint state from DB + existing results
        var checkpoint = new CheckpointState();
        checkpoint.Load(op.CheckpointJson);

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processedUsernames = await db.DeveloperResults
                .Where(r => r.OperationId == op.Id)
                .Select(r => r.Username)
                .ToListAsync(CancellationToken.None);
            checkpoint.SeedProcessed(processedUsernames);
        }

        // Background task: sync PipelineStats → DB every 2 seconds
        using var statsTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var statsSyncTask = Task.Run(async () =>
        {
            try
            {
                while (await statsTimer.WaitForNextTickAsync(ct))
                {
                    await SyncStatsAsync(op.Id, stats, checkpoint);
                }
            }
            catch (OperationCanceledException) { /* expected on stop */ }
        }, CancellationToken.None);

        try
        {
            var keywords  = JsonSerializer.Deserialize<string[]>(op.KeywordsJson) ?? Array.Empty<string>();
            var locations = JsonSerializer.Deserialize<string[]>(op.LocationsJson) ?? Array.Empty<string>();

            var log = _loggerFactory;

            var dbWriter     = new DbWriterService(_scopeFactory, log.CreateLogger<DbWriterService>());
            var searchSvc    = new GitHubSearchService(_httpFactory, _githubOptions, op.GitHubToken, keywords, locations, op.MinFollowers, checkpoint, stats, log.CreateLogger<GitHubSearchService>());
            var profileSvc   = new ProfileParserService(_httpFactory, _githubOptions, stats, log.CreateLogger<ProfileParserService>());
            var emailSvc     = new EmailExtractionService(stats, log.CreateLogger<EmailExtractionService>());

            var searchWorker     = new SearchWorker(searchSvc, checkpoint, stats, log.CreateLogger<SearchWorker>());
            var profileWorker    = new ProfileWorker(profileSvc, checkpoint, stats, log.CreateLogger<ProfileWorker>());
            var extractionWorker = new ExtractionWorker(emailSvc, stats, log.CreateLogger<ExtractionWorker>());

            var orchestrator = new PipelineOrchestrator(
                searchWorker, profileWorker, extractionWorker,
                dbWriter, checkpoint, _githubOptions, op.Id,
                log.CreateLogger<PipelineOrchestrator>());

            await orchestrator.RunAsync(ct);

            await FinalizeOperationAsync(op.Id, stats, checkpoint,
                ct.IsCancellationRequested ? OperationStatus.Cancelled : OperationStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            await FinalizeOperationAsync(op.Id, stats, checkpoint, OperationStatus.Cancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {Id} ({Name}) failed", op.Id, op.Name);
            await FinalizeOperationAsync(op.Id, stats, checkpoint, OperationStatus.Failed);
        }
        finally
        {
            _running.TryRemove(op.Id, out _);
            running.Cts.Dispose();
            // Ensure stats sync task ends
            try { await statsSyncTask; } catch { /* ignored */ }
        }
    }

    private async Task SyncStatsAsync(Guid opId, PipelineStats stats, CheckpointState checkpoint)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var op = await db.Operations.FindAsync(opId);
            if (op is null) return;

            op.ProfilesScanned = stats.ProfilesScanned;
            op.EmailsFound     = stats.EmailsFound;
            op.Failures        = stats.Failures;
            op.CurrentUser     = stats.CurrentUser;
            op.CurrentQuery    = stats.CurrentQuery;
            op.CheckpointJson  = checkpoint.Serialize();
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stats sync failed for op {Id}", opId);
        }
    }

    private async Task FinalizeOperationAsync(
        Guid opId, PipelineStats stats, CheckpointState checkpoint, OperationStatus status)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var op = await db.Operations.FindAsync(opId);
            if (op is null) return;

            op.Status          = status;
            op.CompletedAt     = DateTime.UtcNow;
            op.ProfilesScanned = stats.ProfilesScanned;
            op.EmailsFound     = stats.EmailsFound;
            op.Failures        = stats.Failures;
            op.CurrentUser     = null;
            op.CurrentQuery    = null;
            op.CheckpointJson  = checkpoint.Serialize();
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finalize operation {Id}", opId);
        }
    }

    /// <summary>
    /// On startup: mark any operations that were left in Running state as Cancelled.
    /// This prevents ghost "Running" state after an app restart.
    /// </summary>
    public async Task RecoverInterruptedOperationsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var interrupted = await db.Operations
            .Where(o => o.Status == OperationStatus.Running)
            .ToListAsync();

        foreach (var op in interrupted)
        {
            op.Status      = OperationStatus.Cancelled;
            op.CompletedAt = DateTime.UtcNow;
            op.CurrentUser  = null;
            op.CurrentQuery = null;
        }

        if (interrupted.Count > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogWarning(
                "Recovered {Count} interrupted operation(s) to Cancelled state.",
                interrupted.Count);
        }
    }
}
