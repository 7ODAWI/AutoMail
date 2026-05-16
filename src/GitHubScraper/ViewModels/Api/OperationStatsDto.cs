using GitHubScraper.Models.Enums;

namespace GitHubScraper.ViewModels.Api;

public sealed class OperationStatsDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public long ProfilesScanned { get; set; }
    public long EmailsFound { get; set; }
    public long Failures { get; set; }
    public string? CurrentUser { get; set; }
    public string? CurrentQuery { get; set; }
    public int RateLimitRemaining { get; set; }
    public long RateLimitResetEpoch { get; set; }
    public int RequestsPerMinute { get; set; }
}
