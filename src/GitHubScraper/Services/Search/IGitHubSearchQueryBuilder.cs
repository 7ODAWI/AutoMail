namespace GitHubScraper.Services.Search;

public interface IGitHubSearchQueryBuilder
{
    GitHubBuiltQuery Build(GitHubQueryBuildRequest request);
    ValueTask<GitHubBuiltQuery> BuildAsync(GitHubQueryBuildRequest request, CancellationToken ct = default);
    string BuildWebSearchUrl(string webBaseUrl, string query, int page, string type = "users");
}
