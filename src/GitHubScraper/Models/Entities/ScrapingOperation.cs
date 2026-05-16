using GitHubScraper.Models.Enums;

namespace GitHubScraper.Models.Entities;

public sealed class ScrapingOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? GitHubToken { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Idle;

    // Filters stored as JSON arrays
    public string KeywordsJson { get; set; } = "[]";
    public string LocationsJson { get; set; } = "[]";
    public int MinFollowers { get; set; } = 0;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Live stats (periodically synced from PipelineStats)
    public long ProfilesScanned { get; set; }
    public long EmailsFound { get; set; }
    public long Failures { get; set; }
    public string? CurrentUser { get; set; }
    public string? CurrentQuery { get; set; }

    // Resume state: JSON-serialized CheckpointData
    public string? CheckpointJson { get; set; }

    // Navigation
    public ICollection<DeveloperResult> Results { get; set; } = new List<DeveloperResult>();
}
