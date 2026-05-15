using GitHubEmailScraper.Models;

namespace GitHubEmailScraper.Services;

public interface ICsvWriterService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task WriteAsync(DeveloperRecord record, CancellationToken ct = default);
}
