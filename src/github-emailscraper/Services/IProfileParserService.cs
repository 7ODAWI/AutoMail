using GitHubEmailScraper.Models;

namespace GitHubEmailScraper.Services;

public interface IProfileParserService
{
    Task<GitHubUser?> GetUserAsync(string username, CancellationToken ct = default);
}
