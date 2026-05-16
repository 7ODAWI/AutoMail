using System.Threading.Channels;
using GitHubScraper.Models.Pipeline;
using GitHubScraper.Services;

namespace GitHubScraper.Pipeline;

/// <summary>
/// One of N concurrent consumers from the username channel.
/// Fetches the GitHub profile HTML for each username and writes it to the profile channel.
/// </summary>
public sealed class ProfileWorker
{
    private readonly IProfileParserService _profileParser;
    private readonly CheckpointState _checkpoint;
    private readonly PipelineStats _stats;
    private readonly ILogger<ProfileWorker> _logger;

    public ProfileWorker(
        IProfileParserService profileParser,
        CheckpointState checkpoint,
        PipelineStats stats,
        ILogger<ProfileWorker> logger)
    {
        _profileParser = profileParser;
        _checkpoint = checkpoint;
        _stats = stats;
        _logger = logger;
    }

    public async Task RunAsync(
        ChannelReader<string> usernameReader,
        ChannelWriter<GitHubUser> profileWriter,
        int workerId,
        CancellationToken ct)
    {
        _logger.LogInformation("Profile worker {Id} started.", workerId);

        try
        {
            await foreach (var username in usernameReader.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested) break;

                // Double-check in case another worker already processed it
                if (_checkpoint.IsProcessed(username))
                {
                    _stats.IncrementDuplicatesSkipped();
                    continue;
                }

                var user = await _profileParser.GetUserAsync(username, ct);

                // Mark processed regardless of result to avoid re-fetching 404s
                _checkpoint.MarkProcessed(username);

                if (user is not null)
                    await profileWriter.WriteAsync(user, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Profile worker {Id} cancelled.", workerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile worker {Id} encountered an unhandled error.", workerId);
        }

        _logger.LogInformation("Profile worker {Id} completed.", workerId);
    }
}
