using GitHubScraper.Models.Pipeline;

namespace GitHubScraper.Services;

public interface IEmailExtractionService
{
    Task<IReadOnlyList<EmailResult>> ExtractAsync(GitHubUser user, CancellationToken ct = default);
}
