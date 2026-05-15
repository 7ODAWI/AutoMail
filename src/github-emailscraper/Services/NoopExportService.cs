using GitHubEmailScraper.Models;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

public sealed class NoopExportService : ISqliteExportService
{
    private readonly ILogger<NoopExportService> _logger;

    public NoopExportService(ILogger<NoopExportService> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("No-op export service initialized.");
        return Task.CompletedTask;
    }

    public Task UpsertAsync(DeveloperRecord record, CancellationToken ct = default)
    {
        // intentionally no-op
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
