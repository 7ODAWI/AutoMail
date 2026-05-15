namespace GitHubEmailScraper.Services;

public interface IGitHubSearchService
{
    IAsyncEnumerable<string> SearchUsernamesAsync(CancellationToken ct = default);
}
