using GitHubEmailScraper.Models;

namespace GitHubEmailScraper.Services;

public interface ISqliteExportService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task UpsertAsync(DeveloperRecord record, CancellationToken ct = default);
}
