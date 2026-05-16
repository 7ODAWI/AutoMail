namespace GitHubScraper.Services.Search;

public sealed class GitHubSearchQueryException : Exception
{
    public GitHubSearchQueryException(string message)
        : base(message)
    {
    }
}
