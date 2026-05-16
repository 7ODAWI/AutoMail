using GitHubScraper.Services.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitHubScraper.Tests.Services.Search;

public sealed class GitHubSearchExpressionParserTests
{
    private static GitHubSearchExpressionParser CreateParser() =>
        new(NullLogger<GitHubSearchExpressionParser>.Instance);

    [Fact]
    public void Parse_Respects_OperatorPrecedence()
    {
        var parser = CreateParser();

        var parsed = parser.Parse("react OR vue AND node");

        var root = Assert.IsType<GitHubBinaryNode>(parsed.Root);
        Assert.Equal(GitHubBooleanOperator.Or, root.Operator);

        Assert.IsType<GitHubTermNode>(root.Left);
        var right = Assert.IsType<GitHubBinaryNode>(root.Right);
        Assert.Equal(GitHubBooleanOperator.And, right.Operator);
    }

    [Fact]
    public void Parse_Supports_NestedGroups_And_Not()
    {
        var parser = CreateParser();

        var parsed = parser.Parse("(\"full stack\" OR backend) AND NOT student");

        var root = Assert.IsType<GitHubBinaryNode>(parsed.Root);
        Assert.Equal(GitHubBooleanOperator.And, root.Operator);
        Assert.IsType<GitHubBinaryNode>(root.Left);
        Assert.IsType<GitHubUnaryNode>(root.Right);
    }

    [Fact]
    public void Parse_Throws_For_UnbalancedParenthesis()
    {
        var parser = CreateParser();

        var ex = Assert.Throws<GitHubSearchQueryException>(() => parser.Parse("(react AND node"));

        Assert.Contains("Missing closing ')'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_For_DanglingOperator()
    {
        var parser = CreateParser();

        var ex = Assert.Throws<GitHubSearchQueryException>(() => parser.Parse("react AND"));

        Assert.Contains("Operator 'AND'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Validates_QualifierValues()
    {
        var parser = CreateParser();

        var ex = Assert.Throws<GitHubSearchQueryException>(() => parser.Parse("react AND followers:abc"));

        Assert.Contains("expects a numeric value", ex.Message, StringComparison.Ordinal);
    }
}
