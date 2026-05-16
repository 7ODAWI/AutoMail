namespace GitHubScraper.Services.Search;

public enum GitHubBooleanOperator
{
    And,
    Or
}

public enum GitHubUnaryOperator
{
    Not
}

public abstract record GitHubQueryNode;

public sealed record GitHubTermNode(
    string Value,
    bool IsPhrase,
    GitHubQualifierToken? Qualifier = null) : GitHubQueryNode;

public sealed record GitHubUnaryNode(
    GitHubUnaryOperator Operator,
    GitHubQueryNode Operand) : GitHubQueryNode;

public sealed record GitHubBinaryNode(
    GitHubBooleanOperator Operator,
    GitHubQueryNode Left,
    GitHubQueryNode Right) : GitHubQueryNode;

public sealed record GitHubQualifierToken(
    string Name,
    string Value,
    bool WasQuoted);

public sealed record GitHubParsedExpression(
    string OriginalExpression,
    GitHubQueryNode Root,
    IReadOnlyList<string> Tokens,
    string DebugTree);

public sealed record GitHubQueryBuildRequest(
    string? Expression,
    IReadOnlyDictionary<string, string>? Qualifiers = null);

public sealed record GitHubBuiltQuery(
    string Query,
    string EncodedQuery,
    string DebugTree);
