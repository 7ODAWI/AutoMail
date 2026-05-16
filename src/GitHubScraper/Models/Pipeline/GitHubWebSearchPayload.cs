using System.Text.Json.Serialization;

namespace GitHubScraper.Models.Pipeline;

public sealed class GitHubWebSearchPayload
{
    [JsonPropertyName("payload")]
    public GitHubWebPayload? Payload { get; set; }
}

public sealed class GitHubWebPayload
{
    [JsonPropertyName("results")]
    public GitHubWebUser[] Results { get; set; } = Array.Empty<GitHubWebUser>();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("result_count")]
    public int ResultCount { get; set; }
}

public sealed class GitHubWebUser
{
    [JsonPropertyName("login")]
    public string? Login { get; set; }

    [JsonPropertyName("display_login")]
    public string? DisplayLogin { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("followers")]
    public int Followers { get; set; }

    [JsonPropertyName("repos")]
    public int Repos { get; set; }
}
