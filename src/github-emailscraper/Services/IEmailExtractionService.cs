using GitHubEmailScraper.Models;

namespace GitHubEmailScraper.Services;

public interface IEmailExtractionService
{
    Task<IReadOnlyList<EmailResult>> ExtractAsync(GitHubUser user, CancellationToken ct = default);
}
