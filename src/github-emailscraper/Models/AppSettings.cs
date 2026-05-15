namespace GitHubEmailScraper.Models;

public sealed class AppSettings
{
    public GitHubOptions GitHub { get; init; } = new();
    public SearchOptions Search { get; init; } = new();
    public OutputOptions Output { get; init; } = new();
    public ProxyOptions Proxies { get; init; } = new();
}

public sealed class GitHubOptions
{
    public string PersonalAccessToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.github.com";
    public string WebBaseUrl { get; init; } = "https://github.com";
    public int SearchDelayMs { get; init; } = 2200;
    public int ProfileDelayMs { get; init; } = 500;
    public int WebPageDelayMs { get; init; } = 800;
    public int MaxConcurrentProfileWorkers { get; init; } = 2;
    public int MaxConcurrentExtractionWorkers { get; init; } = 2;
}

public sealed class SearchOptions
{
    public string[] Keywords { get; init; } = Array.Empty<string>();
    public string[] Locations { get; init; } = Array.Empty<string>();
    public int MinFollowers { get; init; } = 0;
}

public sealed class OutputOptions
{
    public string CsvPath { get; init; } = "output/developers.csv";
    public string CheckpointPath { get; init; } = "output/checkpoint.json";
    public bool EnableSqlite { get; init; } = false;
    public string SqlitePath { get; init; } = "output/developers.db";
    public string InvalidEmailsCsvPath { get; init; } = "output/invalid_emails.csv";
    public bool EnableSqlServer { get; init; } = false;
    public string SqlServerConnectionString { get; init; } = string.Empty;
}

public sealed class ProxyOptions
{
    public bool Enabled { get; init; } = false;
    public string[] List { get; init; } = Array.Empty<string>();
}
