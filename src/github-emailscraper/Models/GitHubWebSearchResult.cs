using System.Text.Json.Serialization;

namespace GitHubEmailScraper.Models;

/// <summary>
/// Top-level envelope returned by https://github.com/search?q=...&amp;type=users&amp;p=N
/// when Accept: application/json is sent.
/// </summary>
public sealed class GitHubWebSearchResult
{
    [JsonPropertyName("payload")]
    public GitHubWebSearchPayload? Payload { get; init; }
}

public sealed class GitHubWebSearchPayload
{
    [JsonPropertyName("results")]
    public GitHubWebUser[] Results { get; init; } = [];

    /// <summary>Number of pages available (capped at 100 by GitHub).</summary>
    [JsonPropertyName("page_count")]
    public int PageCount { get; init; }

    /// <summary>Total number of matching users across all pages.</summary>
    [JsonPropertyName("result_count")]
    public int ResultCount { get; init; }
}

public sealed class GitHubWebUser
{
    [JsonPropertyName("login")]
    public string Login { get; init; } = "";

    /// <summary>Use for profile URL: https://github.com/{DisplayLogin}</summary>
    [JsonPropertyName("display_login")]
    public string? DisplayLogin { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("profile_bio")]
    public string? ProfileBio { get; init; }

    [JsonPropertyName("followers")]
    public int Followers { get; init; }

    [JsonPropertyName("repos")]
    public int Repos { get; init; }
}
