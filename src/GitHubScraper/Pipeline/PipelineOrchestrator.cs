using System.Threading.Channels;
using GitHubScraper.Models.Entities;
using GitHubScraper.Models.Pipeline;
using GitHubScraper.Models.Settings;
using GitHubScraper.Services;

namespace GitHubScraper.Pipeline;

/// <summary>
/// Wires up the three-stage producer–consumer pipeline:
///
///   SearchWorker (×1) ──► username channel (cap 100)
///       ──► ProfileWorker (×N) ──► profile channel (cap 50)
///           ──► ExtractionWorker (×N) ──► record channel (cap 50)
///               ──► DbWriterService (single reader)
///
/// Channels propagate completion automatically.
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly SearchWorker _searchWorker;
    private readonly ProfileWorker _profileWorker;
    private readonly ExtractionWorker _extractionWorker;
    private readonly IDbWriterService _dbWriter;
    private readonly CheckpointState _checkpoint;
    private readonly GitHubOptions _options;
    private readonly Guid _operationId;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        SearchWorker searchWorker,
        ProfileWorker profileWorker,
        ExtractionWorker extractionWorker,
        IDbWriterService dbWriter,
        CheckpointState checkpoint,
        GitHubOptions options,
        Guid operationId,
        ILogger<PipelineOrchestrator> logger)
    {
        _searchWorker     = searchWorker;
        _profileWorker    = profileWorker;
        _extractionWorker = extractionWorker;
        _dbWriter         = dbWriter;
        _checkpoint       = checkpoint;
        _options          = options;
        _operationId      = operationId;
        _logger           = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
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

        var recordChannel = Channel.CreateBounded<DeveloperResult>(
            new BoundedChannelOptions(50)
            {
                FullMode     = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });

        var profileWorkers    = _options.MaxConcurrentProfileWorkers;
        var extractionWorkers = _options.MaxConcurrentExtractionWorkers;

        _logger.LogInformation(
            "Pipeline started: {P} profile workers, {E} extraction workers.",
            profileWorkers, extractionWorkers);

        try
        {
            var searchTask = _searchWorker.RunAsync(usernameChannel.Writer, ct);

            var profileTasks = Enumerable
                .Range(1, profileWorkers)
                .Select(id => _profileWorker.RunAsync(
                    usernameChannel.Reader, profileChannel.Writer, id, ct))
                .ToArray();

            var profileDrainTask = Task.WhenAll(profileTasks)
                .ContinueWith(_ => profileChannel.Writer.TryComplete(),
                    CancellationToken.None, TaskContinuationOptions.None,
                    TaskScheduler.Default);

            var extractionTasks = Enumerable
                .Range(1, extractionWorkers)
                .Select(id => _extractionWorker.RunAsync(
                    profileChannel.Reader, recordChannel.Writer, id, _operationId, ct))
                .ToArray();

            var extractionDrainTask = Task.WhenAll(extractionTasks)
                .ContinueWith(_ => recordChannel.Writer.TryComplete(),
                    CancellationToken.None, TaskContinuationOptions.None,
                    TaskScheduler.Default);

            var dbWriteTask = WriteRecordsAsync(recordChannel.Reader, ct);

            await Task.WhenAll(
                searchTask,
                profileDrainTask,
                extractionDrainTask,
                dbWriteTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled pipeline error.");
        }
        finally
        {
            _logger.LogInformation(
                "Pipeline complete. Processed {Count} profiles.",
                _checkpoint.TotalProcessed);
        }
    }

    private async Task WriteRecordsAsync(ChannelReader<DeveloperResult> reader, CancellationToken ct)
    {
        await foreach (var record in reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                await _dbWriter.WriteAsync(record, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB write failed for {Username}", record.Username);
            }
        }
    }
}
