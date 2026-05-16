using GitHubScraper.Models.Pipeline;

namespace GitHubScraper.Services;

public interface IGitHubSearchService
{
    IAsyncEnumerable<string> SearchUsernamesAsync(CancellationToken ct = default);
}
