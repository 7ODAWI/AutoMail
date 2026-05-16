using System.Text;
using System.Text.RegularExpressions;

namespace GitHubScraper.Services.Search;

/// <summary>
/// Builds canonical and validated GitHub search query strings from boolean expressions
/// and dynamic qualifiers, then generates URL-safe GitHub web search URLs.
/// </summary>
public sealed class GitHubSearchQueryBuilder : IGitHubSearchQueryBuilder
{
    private static readonly Regex NeedsQuotingRegex = new(
        "[\\s()\\\"]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IGitHubSearchExpressionParser _parser;
    private readonly ILogger<GitHubSearchQueryBuilder> _logger;

    public GitHubSearchQueryBuilder(
        IGitHubSearchExpressionParser parser,
        ILogger<GitHubSearchQueryBuilder> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    public GitHubBuiltQuery Build(GitHubQueryBuildRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        GitHubQueryNode? root = null;
        var debugParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Expression))
        {
            var parsed = _parser.Parse(request.Expression);
            root = parsed.Root;
            debugParts.Add(parsed.DebugTree);
        }

        if (request.Qualifiers is not null)
        {
            foreach (var pair in request.Qualifiers)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    continue;

                var qualifierTerm = BuildQualifierNode(pair.Key.Trim(), pair.Value.Trim());
                root = root is null
                    ? qualifierTerm
                    : new GitHubBinaryNode(GitHubBooleanOperator.And, root, qualifierTerm);
            }
        }

        if (root is null)
            throw new GitHubSearchQueryException("At least one search term or qualifier is required.");

        var rendered = Render(root, parentPrecedence: 0);
        var encoded = Uri.EscapeDataString(rendered);

        _logger.LogDebug("Built query: {Query}", rendered);

        return new GitHubBuiltQuery(
            rendered,
            encoded,
            string.Join(Environment.NewLine, debugParts));
    }

    public ValueTask<GitHubBuiltQuery> BuildAsync(GitHubQueryBuildRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Build(request));
    }

    public string BuildWebSearchUrl(string webBaseUrl, string query, int page, string type = "users")
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new GitHubSearchQueryException("Query cannot be empty when building URL.");

        var normalizedBase = string.IsNullOrWhiteSpace(webBaseUrl)
            ? "https://github.com"
            : webBaseUrl.TrimEnd('/');

        var encodedQuery = Uri.EscapeDataString(query);
        var encodedType = Uri.EscapeDataString(type);

        return $"{normalizedBase}/search?q={encodedQuery}&type={encodedType}&p={Math.Max(1, page)}&ref=advsearch";
    }

    private static GitHubTermNode BuildQualifierNode(string name, string rawValue)
    {
        var unwrapped = TryUnquote(rawValue, out var wasQuoted);
        var qualifier = new GitHubQualifierToken(name, unwrapped, wasQuoted);
        return new GitHubTermNode($"{name}:{rawValue}", false, qualifier);
    }

    private static string Render(GitHubQueryNode node, int parentPrecedence)
    {
        switch (node)
        {
            case GitHubTermNode term:
                return RenderTerm(term);

            case GitHubUnaryNode unary:
            {
                const int precedence = 3;
                var operand = Render(unary.Operand, precedence);
                var text = $"NOT {operand}";
                return WrapIfNeeded(text, precedence, parentPrecedence);
            }

            case GitHubBinaryNode binary:
            {
                var precedence = binary.Operator == GitHubBooleanOperator.And ? 2 : 1;
                var op = binary.Operator == GitHubBooleanOperator.And ? "AND" : "OR";
                var left = Render(binary.Left, precedence);
                var right = Render(binary.Right, precedence);
                var text = $"{left} {op} {right}";
                return WrapIfNeeded(text, precedence, parentPrecedence);
            }

            default:
                throw new InvalidOperationException($"Unsupported node type: {node.GetType().Name}");
        }
    }

    private static string WrapIfNeeded(string text, int nodePrecedence, int parentPrecedence)
    {
        if (nodePrecedence < parentPrecedence)
            return $"({text})";

        return text;
    }

    private static string RenderTerm(GitHubTermNode term)
    {
        if (term.Qualifier is not null)
        {
            var q = term.Qualifier;
            var value = q.WasQuoted || NeedsQuotingRegex.IsMatch(q.Value)
                ? Quote(EscapeQuoted(q.Value))
                : q.Value;

            return $"{q.Name}:{value}";
        }

        if (term.IsPhrase || NeedsQuotingRegex.IsMatch(term.Value))
            return Quote(EscapeQuoted(term.Value));

        return term.Value;
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string EscapeQuoted(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string TryUnquote(string value, out bool wasQuoted)
    {
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            wasQuoted = true;
            return value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        wasQuoted = false;
        return value;
    }
}
