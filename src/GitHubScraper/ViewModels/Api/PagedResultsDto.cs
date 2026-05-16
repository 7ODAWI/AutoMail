namespace GitHubScraper.ViewModels.Api;

public sealed class PagedResultsDto
{
    public List<ResultRowDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public sealed class ResultRowDto
{
    public string Username { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? EmailConfidence { get; set; }
    public int Followers { get; set; }
    public int Repos { get; set; }
    public string? ProfileUrl { get; set; }
    public string? Website { get; set; }
    public DateTime FoundAtUtc { get; set; }
}
