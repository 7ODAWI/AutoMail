using GitHubScraper.Services.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitHubScraper.Tests.Services.Search;

public sealed class GitHubSearchQueryBuilderTests
{
    private static GitHubSearchQueryBuilder CreateBuilder()
    {
        var parser = new GitHubSearchExpressionParser(NullLogger<GitHubSearchExpressionParser>.Instance);
        return new GitHubSearchQueryBuilder(parser, NullLogger<GitHubSearchQueryBuilder>.Instance);
    }

    [Fact]
    public void Build_Composes_Expression_With_Qualifiers()
    {
        var builder = CreateBuilder();

        var built = builder.Build(new GitHubQueryBuildRequest(
            "(\"software engineer\" OR developer) AND python",
            new Dictionary<string, string>
            {
                ["location"] = "United States",
                ["followers"] = ">=20"
            }));

        Assert.Equal("(\"software engineer\" OR developer) AND python AND location:\"United States\" AND followers:>=20", built.Query);
        Assert.NotEmpty(built.EncodedQuery);
    }

    [Fact]
    public void Build_Supports_Not_As_ImplicitAnd()
    {
        var builder = CreateBuilder();

        var built = builder.Build(new GitHubQueryBuildRequest("react NOT angular"));

        Assert.Equal("react AND NOT angular", built.Query);
    }

    [Fact]
    public void BuildWebSearchUrl_Produces_GitHubCompatibleUrl()
    {
        var builder = CreateBuilder();
        var url = builder.BuildWebSearchUrl("https://github.com", "react AND location:\"United States\"", 3);

        Assert.Equal(
            "https://github.com/search?q=react%20AND%20location%3A%22United%20States%22&type=users&p=3&ref=advsearch",
            url);
    }
}
