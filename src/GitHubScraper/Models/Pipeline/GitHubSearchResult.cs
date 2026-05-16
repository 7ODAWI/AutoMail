using System.Text.Json.Serialization;

namespace GitHubScraper.Models.Pipeline;

public sealed class GitHubSearchResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("incomplete_results")]
    public bool IncompleteResults { get; init; }

    [JsonPropertyName("items")]
    public GitHubSearchItem[] Items { get; init; } = Array.Empty<GitHubSearchItem>();
}

public sealed class GitHubSearchItem
{
    [JsonPropertyName("login")]
    public string Login { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; init; }
}
