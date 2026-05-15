namespace GitHubEmailScraper.Services;

public interface IDashboardService
{
    void Start(CancellationToken ct);
    void Stop();
}
