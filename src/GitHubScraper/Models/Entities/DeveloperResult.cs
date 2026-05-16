namespace GitHubScraper.Models.Entities;

public sealed class DeveloperResult
{
    public int Id { get; set; }
    public Guid OperationId { get; set; }

    public string Username { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? EmailSource { get; set; }
    public string? EmailConfidence { get; set; }
    public string? Website { get; set; }
    public int Followers { get; set; }
    public int Repos { get; set; }
    public string? ProfileUrl { get; set; }
    public DateTime FoundAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ScrapingOperation Operation { get; set; } = null!;
}
