namespace GitHubScraper.Models.Pipeline;

public enum EmailConfidence
{
    Low,
    Medium,
    High
}

public sealed class EmailResult
{
    public string Email { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public EmailConfidence Confidence { get; init; }
    public bool IsValid { get; init; }
}
