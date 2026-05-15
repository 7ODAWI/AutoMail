using System.Threading.Channels;
using GitHubEmailScraper.Models;
using GitHubEmailScraper.Services;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Pipeline;

/// <summary>
/// Wires up the three-stage producer–consumer pipeline:
///
///   SearchWorker (×1) ──► username channel (cap 100)
///       ──► ProfileWorker (×N) ──► profile channel (cap 50)
///           ──► ExtractionWorker (×N) ──► record channel (cap 50)
///               ──► CSV + SQLite writer
///
/// Channels propagate completion automatically: each stage completes its output
/// channel once all tasks writing to it have finished.
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly SearchWorker      _searchWorker;
    private readonly ProfileWorker     _profileWorker;
    private readonly ExtractionWorker  _extractionWorker;

    private readonly ICsvWriterService     _csvWriter;
    private readonly ISqliteExportService  _sqliteExport;
    private readonly ICheckpointService    _checkpoint;
    private readonly IDashboardService     _dashboard;
    private readonly AppSettings           _settings;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        SearchWorker      searchWorker,
        ProfileWorker     profileWorker,
        ExtractionWorker  extractionWorker,
        ICsvWriterService    csvWriter,
        ISqliteExportService sqliteExport,
        ICheckpointService   checkpoint,
        IDashboardService    dashboard,
        AppSettings          settings,
        ILogger<PipelineOrchestrator> logger)
    {
        _searchWorker     = searchWorker;
        _profileWorker    = profileWorker;
        _extractionWorker = extractionWorker;
        _csvWriter        = csvWriter;
        _sqliteExport     = sqliteExport;
        _checkpoint       = checkpoint;
        _dashboard        = dashboard;
        _settings         = settings;
        _logger           = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // ── 1. Bootstrap ──────────────────────────────────────────────────────
        await _checkpoint.LoadAsync(ct);
        await _csvWriter.InitializeAsync(ct);
        await _sqliteExport.InitializeAsync(ct);

        // ── 2. Create bounded channels ────────────────────────────────────────
        var usernameChannel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(100)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

        var profileChannel = Channel.CreateBounded<GitHubUser>(
            new BoundedChannelOptions(50)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });

        var recordChannel = Channel.CreateBounded<DeveloperRecord>(
            new BoundedChannelOptions(50)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });

        // ── 3. Start dashboard ────────────────────────────────────────────────
        _dashboard.Start(ct);

        var profileWorkers    = _settings.GitHub.MaxConcurrentProfileWorkers;
        var extractionWorkers = _settings.GitHub.MaxConcurrentExtractionWorkers;

        _logger.LogInformation(
            "Pipeline started: {P} profile workers, {E} extraction workers.",
            profileWorkers, extractionWorkers);

        try
        {
            // Stage 1 — single search producer
            var searchTask = _searchWorker.RunAsync(usernameChannel.Writer, ct);

            // Stage 2 — N profile workers (fan-out over the same reader)
            var profileTasks = Enumerable
                .Range(1, profileWorkers)
                .Select(id => _profileWorker.RunAsync(
                    usernameChannel.Reader, profileChannel.Writer, id, ct))
                .ToArray();

            // Complete profile channel when ALL profile workers finish writing
            var profileDrainTask = Task.WhenAll(profileTasks)
                .ContinueWith(_ => profileChannel.Writer.TryComplete(),
                    CancellationToken.None, TaskContinuationOptions.None,
                    TaskScheduler.Default);

            // Stage 3 — N extraction workers
            var extractionTasks = Enumerable
                .Range(1, extractionWorkers)
                .Select(id => _extractionWorker.RunAsync(
                    profileChannel.Reader, recordChannel.Writer, id, ct))
                .ToArray();

            // Complete record channel when ALL extraction workers finish writing
            var extractionDrainTask = Task.WhenAll(extractionTasks)
                .ContinueWith(_ => recordChannel.Writer.TryComplete(),
                    CancellationToken.None, TaskContinuationOptions.None,
                    TaskScheduler.Default);

            // Stage 4 — single CSV/SQLite writer (SingleReader = true on recordChannel)
            var csvTask = WriteRecordsAsync(recordChannel.Reader, ct);

            // Wait for the full pipeline to drain
            await Task.WhenAll(
                searchTask,
                profileDrainTask,
                extractionDrainTask,
                csvTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled by user.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled pipeline error.");
        }
        finally
        {
            _dashboard.Stop();
            await _checkpoint.SaveAsync(CancellationToken.None);
            _logger.LogInformation(
                "Pipeline complete. Processed {Count} profiles.",
                _checkpoint.TotalProcessed);
        }
    }

    private async Task WriteRecordsAsync(
        ChannelReader<DeveloperRecord> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var record in reader.ReadAllAsync(ct))
            {
                await _csvWriter.WriteAsync(record, ct);
                await _sqliteExport.UpsertAsync(record, ct);

                _logger.LogDebug("Saved: {Username} | {Email}", record.Username, record.Email);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Record writer cancelled.");
        }
    }
}
