using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using GitHubEmailScraper.Infrastructure;
using GitHubEmailScraper.Models;
using GitHubEmailScraper.Pipeline;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Singleton that creates and manages independent scraping operations.
/// Each operation gets its own isolated set of pipeline service instances
/// so keyword/location filters, checkpoints, and CSV files never cross-contaminate.
/// </summary>
public sealed class OperationManager
{
    private readonly ConcurrentDictionary<Guid, ScrapingOperation>         _ops        = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource>   _ctsSources = new();

    private readonly IHttpClientFactory        _httpFactory;
    private readonly ILoggerFactory            _loggerFactory;
    private readonly AppSettings               _baseSettings;
    private readonly ILogger<OperationManager> _logger;

    /// <summary>Raised on any state change — UI components subscribe to trigger re-render.</summary>
    public event Action? StateChanged;

    public OperationManager(
        IHttpClientFactory        httpFactory,
        ILoggerFactory            loggerFactory,
        AppSettings               baseSettings,
        ILogger<OperationManager> logger)
    {
        _httpFactory   = httpFactory;
        _loggerFactory = loggerFactory;
        _baseSettings  = baseSettings;
        _logger        = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public IReadOnlyList<ScrapingOperation> GetAll() =>
        _ops.Values.OrderByDescending(o => o.CreatedAt).ToList();

    public ScrapingOperation? Get(Guid id) => _ops.GetValueOrDefault(id);

    // ── Mutations ─────────────────────────────────────────────────────────────

    public ScrapingOperation Create(string name, OperationFilters filters)
    {
        var op = new ScrapingOperation { Name = name, Filters = filters };
        _ops[op.Id] = op;
        NotifyChanged();
        return op;
    }

    public void Start(Guid id)
    {
        if (!_ops.TryGetValue(id, out var op)) return;
        if (op.Status == OperationStatus.Running) return;

        // Reset live stats for a fresh run
        op.Status         = OperationStatus.Running;
        op.StartedAt      = DateTime.UtcNow;
        op.CompletedAt    = null;
        op.ProfilesScanned = 0;
        op.EmailsFound    = 0;
        op.Failures       = 0;
        op.CurrentUser    = string.Empty;
        op.CurrentQuery   = string.Empty;

        var cts = new CancellationTokenSource();
        _ctsSources[id] = cts;

        NotifyChanged();
        _ = Task.Run(() => RunOperationAsync(op, cts.Token));
    }

    public void Stop(Guid id)
    {
        if (_ctsSources.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_ops.TryGetValue(id, out var op) && op.Status == OperationStatus.Running)
        {
            op.Status      = OperationStatus.Cancelled;
            op.CompletedAt = DateTime.UtcNow;
        }

        NotifyChanged();
    }

    public void Delete(Guid id)
    {
        Stop(id);
        _ops.TryRemove(id, out _);
        NotifyChanged();
    }

    /// <summary>Reads discovered emails from the operation's CSV output file.</summary>
    public IReadOnlyList<DeveloperRecord> GetResults(Guid id)
    {
        if (!_ops.TryGetValue(id, out var op) || !File.Exists(op.CsvPath))
            return Array.Empty<DeveloperRecord>();

        try
        {
            // FileShare.ReadWrite so we can read while the pipeline still has the file open
            using var fs     = new FileStream(op.CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            using var csv    = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<DeveloperRecord>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read results for operation {Id}", id);
            return Array.Empty<DeveloperRecord>();
        }
    }

    // ── Execution ─────────────────────────────────────────────────────────────

    private async Task RunOperationAsync(ScrapingOperation op, CancellationToken ct)
    {
        var opSettings = BuildSettings(op);
        var stats      = new PipelineStats();

        Directory.CreateDirectory(op.OutputDir);

        // Background task: sync PipelineStats → op model so the UI reflects live progress
        using var statsTimer  = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var statsSyncTask = Task.Run(async () =>
        {
            try
            {
                while (await statsTimer.WaitForNextTickAsync(ct))
                {
                    op.ProfilesScanned = stats.ProfilesScanned;
                    op.EmailsFound     = stats.EmailsFound;
                    op.Failures        = stats.Failures;
                    op.CurrentUser     = stats.CurrentUser;
                    op.CurrentQuery    = stats.CurrentQuery;
                    NotifyChanged();
                }
            }
            catch (OperationCanceledException) { /* expected on stop */ }
        }, CancellationToken.None);

        try
        {
            var log = _loggerFactory;

            // Build all services fresh for this operation (fully isolated)
            var checkpoint    = new CheckpointService(opSettings, log.CreateLogger<CheckpointService>());
            var csvWriter     = new CsvWriterService(opSettings, log.CreateLogger<CsvWriterService>());
            // SQL Server persistence removed: always use No-op export service
            var sqliteExport  = new NoopExportService(log.CreateLogger<NoopExportService>());
            var searchService = new GitHubSearchService(_httpFactory, opSettings, checkpoint, stats, log.CreateLogger<GitHubSearchService>());
            var profileParser = new ProfileParserService(_httpFactory, opSettings, stats, log.CreateLogger<ProfileParserService>());
            var emailService  = new EmailExtractionService(stats, log.CreateLogger<EmailExtractionService>());
            var dashboard     = new DashboardService(stats);  // no-op in web context

            var searchWorker     = new SearchWorker(searchService, checkpoint, stats, log.CreateLogger<SearchWorker>());
            var profileWorker    = new ProfileWorker(profileParser, checkpoint, stats, log.CreateLogger<ProfileWorker>());
            var extractionWorker = new ExtractionWorker(emailService, stats, log.CreateLogger<ExtractionWorker>());

            var orchestrator = new PipelineOrchestrator(
                searchWorker, profileWorker, extractionWorker,
                csvWriter, sqliteExport, checkpoint, dashboard,
                opSettings, log.CreateLogger<PipelineOrchestrator>());

            await orchestrator.RunAsync(ct);

            op.Status = ct.IsCancellationRequested
                ? OperationStatus.Cancelled
                : OperationStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            op.Status = OperationStatus.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {Id} ({Name}) failed", op.Id, op.Name);
            op.Status = OperationStatus.Failed;
        }
        finally
        {
            op.CompletedAt     = DateTime.UtcNow;
            op.ProfilesScanned = stats.ProfilesScanned;
            op.EmailsFound     = stats.EmailsFound;
            op.Failures        = stats.Failures;
            op.CurrentUser     = string.Empty;
            op.CurrentQuery    = string.Empty;
            _ctsSources.TryRemove(op.Id, out _);
            NotifyChanged();
        }

        await statsSyncTask;
    }

    private AppSettings BuildSettings(ScrapingOperation op) => new()
    {
        GitHub = _baseSettings.GitHub,
        Search = new SearchOptions
        {
            Keywords     = op.Filters.Keywords.ToArray(),
            Locations    = op.Filters.Locations.ToArray(),
            MinFollowers = op.Filters.MinFollowers
        },
        Output = new OutputOptions
        {
            CsvPath              = op.CsvPath,
            CheckpointPath       = op.CheckpointPath,
            EnableSqlite         = false,
            SqlitePath           = Path.Combine(op.OutputDir, "developers.db"),
            InvalidEmailsCsvPath = Path.Combine(op.OutputDir, "invalid_emails.csv")
        }
    };

    private void NotifyChanged() => StateChanged?.Invoke();
}
