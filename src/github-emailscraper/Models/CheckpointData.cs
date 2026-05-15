using System.Text.Json.Serialization;

namespace GitHubEmailScraper.Models;

public sealed class CheckpointData
{
    [JsonPropertyName("completedQueries")]
    public HashSet<string> CompletedQueries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("queryPages")]
    public Dictionary<string, int> QueryPages { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("processedUsernames")]
    public HashSet<string> ProcessedUsernames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("totalProcessed")]
    public int TotalProcessed { get; set; }

    [JsonPropertyName("savedAtUtc")]
    public DateTime SavedAtUtc { get; set; }
}
