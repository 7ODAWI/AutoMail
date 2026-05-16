using GitHubScraper.Models.Entities;

namespace GitHubScraper.Services;

public interface IDbWriterService
{
    Task WriteAsync(DeveloperResult record, CancellationToken ct = default);
}
