using System.Threading.Channels;
using GitHubEmailScraper.Models;
using GitHubEmailScraper.Services;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Pipeline;

/// <summary>
/// Single producer: streams usernames from the GitHub Search API into the username channel.
/// Skips usernames already present in the checkpoint.
/// </summary>
public sealed class SearchWorker
{
    private readonly IGitHubSearchService _searchService;
    private readonly ICheckpointService   _checkpoint;
    private readonly PipelineStats        _stats;
    private readonly ILogger<SearchWorker> _logger;

    public SearchWorker(
        IGitHubSearchService searchService,
        ICheckpointService   checkpoint,
        PipelineStats        stats,
        ILogger<SearchWorker> logger)
    {
        _searchService = searchService;
        _checkpoint    = checkpoint;
        _stats         = stats;
        _logger        = logger;
    }

    public async Task RunAsync(ChannelWriter<string> usernameWriter, CancellationToken ct)
    {
        try
        {
            await foreach (var username in _searchService.SearchUsernamesAsync(ct))
            {
                if (ct.IsCancellationRequested) break;

                if (_checkpoint.IsProcessed(username))
                {
                    _stats.IncrementDuplicatesSkipped();
                    continue;
                }

                await usernameWriter.WriteAsync(username, ct);
                _logger.LogDebug("Queued: {Username}", username);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Search worker cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search worker encountered an unhandled error.");
        }
        finally
        {
            // Complete the channel regardless of how we exit so downstream workers drain
            usernameWriter.TryComplete();
            _logger.LogInformation("Search worker done. Total checkpoint entries: {Count}",
                _checkpoint.TotalProcessed);
        }
    }
}
