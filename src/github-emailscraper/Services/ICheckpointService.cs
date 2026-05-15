namespace GitHubEmailScraper.Services;

public interface ICheckpointService
{
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    bool IsProcessed(string username);
    void MarkProcessed(string username);
    int GetLastPage(string query);
    void SetLastPage(string query, int page);
    void MarkQueryCompleted(string query);
    bool IsQueryCompleted(string query);
    int TotalProcessed { get; }
}
