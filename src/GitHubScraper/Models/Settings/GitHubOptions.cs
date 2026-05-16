namespace GitHubScraper.Models.Settings;

public sealed class GitHubOptions
{
    public string BaseUrl { get; init; } = "https://api.github.com";
    public string WebBaseUrl { get; init; } = "https://github.com";
    public int SearchDelayMs { get; init; } = 6500;
    public int ProfileDelayMs { get; init; } = 500;
    public int WebPageDelayMs { get; init; } = 2500;
    public int MaxConcurrentProfileWorkers { get; init; } = 1;
    public int MaxConcurrentExtractionWorkers { get; init; } = 2;
}
