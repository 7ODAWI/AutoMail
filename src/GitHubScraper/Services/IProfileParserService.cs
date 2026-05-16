using GitHubScraper.Models.Pipeline;

namespace GitHubScraper.Services;

public interface IProfileParserService
{
    Task<GitHubUser?> GetUserAsync(string username, CancellationToken ct = default);
}
