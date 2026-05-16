namespace GitHubScraper.Services.Search;

public interface IGitHubSearchExpressionParser
{
    GitHubParsedExpression Parse(string expression);
}
