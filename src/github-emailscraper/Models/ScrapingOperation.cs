namespace GitHubEmailScraper.Models;

public sealed class ScrapingOperation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public OperationStatus Status { get; set; } = OperationStatus.Idle;
    public OperationFilters Filters { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Live stats synced from PipelineStats during execution
    public long ProfilesScanned { get; set; }
    public long EmailsFound { get; set; }
    public long Failures { get; set; }
    public string CurrentUser { get; set; } = string.Empty;
    public string CurrentQuery { get; set; } = string.Empty;

    // Per-operation output paths
    public string OutputDir        => Path.Combine("output", "ops", Id.ToString("N"));
    public string CsvPath          => Path.Combine(OutputDir, "developers.csv");
    public string CheckpointPath   => Path.Combine(OutputDir, "checkpoint.json");
}

public sealed class OperationFilters
{
    public List<string> Keywords     { get; set; } = new();
    public List<string> Locations    { get; set; } = new();
    public int          MinFollowers { get; set; } = 0;
}

public enum OperationStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}
