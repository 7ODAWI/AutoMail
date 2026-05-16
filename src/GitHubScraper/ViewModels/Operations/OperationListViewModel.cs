using GitHubScraper.Models.Enums;

namespace GitHubScraper.ViewModels.Operations;

public sealed class OperationCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public OperationStatus Status { get; set; }
    public long EmailsFound { get; set; }
    public long ProfilesScanned { get; set; }
    public long Failures { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Locations { get; set; } = new();
    public int MinFollowers { get; set; }
    public string? CurrentUser { get; set; }
    public string? CurrentQuery { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class OperationListViewModel
{
    public List<OperationCardDto> Operations { get; set; } = new();
}
