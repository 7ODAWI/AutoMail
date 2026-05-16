using System.Text;

namespace GitHubScraper.Services.Search;

/// <summary>
/// Parses advanced GitHub search expressions with support for nested boolean logic.
/// Precedence is: Parentheses, NOT, AND, OR.
/// </summary>
public sealed class GitHubSearchExpressionParser : IGitHubSearchExpressionParser
{
    private static readonly HashSet<string> SupportedQualifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "location",
        "language",
        "followers",
        "repos",
        "type",
        "created"
    };

    private readonly ILogger<GitHubSearchExpressionParser> _logger;

    public GitHubSearchExpressionParser(ILogger<GitHubSearchExpressionParser> logger)
    {
        _logger = logger;
    }

    public GitHubParsedExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new GitHubSearchQueryException("Search expression cannot be empty.");

        var lexer = new Lexer(expression);
        var tokens = lexer.Tokenize();
        if (tokens.Count == 1 && tokens[0].Type == TokenType.End)
            throw new GitHubSearchQueryException("Search expression cannot be empty.");

        var parser = new Parser(tokens);
        var root = parser.ParseExpression();

        var tokenTexts = tokens
            .Where(t => t.Type != TokenType.End)
            .Select(t => t.Display)
            .ToArray();

        _logger.LogDebug("Parsed search expression. Tokens={TokenCount}", tokenTexts.Length);

        return new GitHubParsedExpression(
            expression,
            root,
            tokenTexts,
            DebugTreeFormatter.Format(root));
    }

    private static bool TryParseQualifier(string raw, out GitHubQualifierToken qualifier)
    {
        qualifier = null!;
        var idx = raw.IndexOf(':');
        if (idx <= 0 || idx >= raw.Length - 1)
            return false;

        var name = raw[..idx].Trim();
        var valueRaw = raw[(idx + 1)..].Trim();
        if (name.Length == 0 || valueRaw.Length == 0)
            return false;

        if (!SupportedQualifiers.Contains(name))
            throw new GitHubSearchQueryException($"Unsupported qualifier '{name}'.");

        var wasQuoted = false;
        string value;

        if (valueRaw.StartsWith('"') && valueRaw.EndsWith('"') && valueRaw.Length >= 2)
        {
            wasQuoted = true;
            value = UnescapeQuotedValue(valueRaw[1..^1]);
        }
        else
        {
            value = valueRaw;
        }

        ValidateQualifierValue(name, value);

        qualifier = new GitHubQualifierToken(name, value, wasQuoted);
        return true;
    }

    private static void ValidateQualifierValue(string qualifierName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new GitHubSearchQueryException($"Qualifier '{qualifierName}' must have a value.");

        if (qualifierName.Equals("followers", StringComparison.OrdinalIgnoreCase)
            || qualifierName.Equals("repos", StringComparison.OrdinalIgnoreCase))
        {
            var numeric = System.Text.RegularExpressions.Regex.IsMatch(
                value,
                "^(>=|<=|>|<)?\\d+$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            if (!numeric)
                throw new GitHubSearchQueryException($"Qualifier '{qualifierName}' expects a numeric value, optionally prefixed with >, <, >=, <=.");
        }

        if (qualifierName.Equals("created", StringComparison.OrdinalIgnoreCase))
        {
            var validCreated = System.Text.RegularExpressions.Regex.IsMatch(
                value,
                "^(>=|<=|>|<)?\\d{4}-\\d{2}-\\d{2}$|^\\d{4}-\\d{2}-\\d{2}\\.\\.\\d{4}-\\d{2}-\\d{2}$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            if (!validCreated)
                throw new GitHubSearchQueryException("Qualifier 'created' expects YYYY-MM-DD, comparator+YYYY-MM-DD, or YYYY-MM-DD..YYYY-MM-DD.");
        }
    }

    private static string UnescapeQuotedValue(string value)
    {
        var sb = new StringBuilder(value.Length);
        var escaped = false;

        foreach (var ch in value)
        {
            if (escaped)
            {
                sb.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            sb.Append(ch);
        }

        if (escaped)
            throw new GitHubSearchQueryException("Invalid escape sequence in quoted string.");

        return sb.ToString();
    }

    private enum TokenType
    {
        Term,
        And,
        Or,
        Not,
        LeftParen,
        RightParen,
        End
    }

    private sealed record Token(TokenType Type, string Value, int Position, bool IsPhrase)
    {
        public string Display => Type == TokenType.Term
            ? Value
            : Type.ToString().ToUpperInvariant();
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private int _idx;

        public Lexer(string text)
        {
            _text = text;
        }

        public List<Token> Tokenize()
        {
            var list = new List<Token>();
            while (!End)
            {
                SkipWhitespace();
                if (End) break;

                var pos = _idx;
                var c = _text[_idx];

                if (c == '(')
                {
                    _idx++;
                    list.Add(new Token(TokenType.LeftParen, "(", pos, false));
                    continue;
                }

                if (c == ')')
                {
                    _idx++;
                    list.Add(new Token(TokenType.RightParen, ")", pos, false));
                    continue;
                }

                if (c == '"')
                {
                    var quoted = ReadQuoted();
                    list.Add(new Token(TokenType.Term, quoted, pos, true));
                    continue;
                }

                var raw = ReadTerm();
                if (string.Equals(raw, "AND", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new Token(TokenType.And, raw, pos, false));
                    continue;
                }

                if (string.Equals(raw, "OR", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new Token(TokenType.Or, raw, pos, false));
                    continue;
                }

                if (string.Equals(raw, "NOT", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new Token(TokenType.Not, raw, pos, false));
                    continue;
                }

                list.Add(new Token(TokenType.Term, raw, pos, false));
            }

            list.Add(new Token(TokenType.End, string.Empty, _idx, false));
            return list;
        }

        private bool End => _idx >= _text.Length;

        private void SkipWhitespace()
        {
            while (!End && char.IsWhiteSpace(_text[_idx]))
                _idx++;
        }

        private string ReadQuoted()
        {
            // Current char is opening quote.
            _idx++;

            var sb = new StringBuilder();
            var escaped = false;

            while (!End)
            {
                var c = _text[_idx++];
                if (escaped)
                {
                    sb.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                    return sb.ToString();

                sb.Append(c);
            }

            throw new GitHubSearchQueryException("Unterminated quoted string.");
        }

        private string ReadTerm()
        {
            var sb = new StringBuilder();
            var sawColon = false;

            while (!End)
            {
                var c = _text[_idx];
                if (char.IsWhiteSpace(c) || c is '(' or ')')
                    break;

                if (c == ':')
                    sawColon = true;

                if (c == '"')
                {
                    if (!sawColon)
                        throw new GitHubSearchQueryException("Quotes inside unqualified term are not allowed. Use full quoted phrase token.");

                    sb.Append(c);
                    _idx++;

                    var escaped = false;
                    while (!End)
                    {
                        var qc = _text[_idx++];
                        sb.Append(qc);

                        if (escaped)
                        {
                            escaped = false;
                            continue;
                        }

                        if (qc == '\\')
                        {
                            escaped = true;
                            continue;
                        }

                        if (qc == '"')
                            break;
                    }

                    if (sb[^1] != '"')
                        throw new GitHubSearchQueryException("Unterminated quote in qualifier value.");

                    continue;
                }

                sb.Append(c);
                _idx++;
            }

            if (sb.Length == 0)
                throw new GitHubSearchQueryException("Invalid token in search expression.");

            return sb.ToString();
        }
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _idx;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        public GitHubQueryNode ParseExpression()
        {
            var expr = ParseOr();
            var trailing = Peek();
            if (trailing.Type != TokenType.End)
                throw Error($"Unexpected token '{trailing.Value}'.");

            return expr;
        }

        private GitHubQueryNode ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenType.Or))
            {
                EnsureOperandAfterOperator("OR");
                var right = ParseAnd();
                left = new GitHubBinaryNode(GitHubBooleanOperator.Or, left, right);
            }

            return left;
        }

        private GitHubQueryNode ParseAnd()
        {
            var left = ParseUnary();

            while (true)
            {
                if (Match(TokenType.And))
                {
                    EnsureOperandAfterOperator("AND");
                    var right = ParseUnary();
                    left = new GitHubBinaryNode(GitHubBooleanOperator.And, left, right);
                    continue;
                }

                if (StartsPrimary(Peek().Type))
                {
                    // Adjacent terms/groups are treated as implicit AND to match GitHub semantics.
                    var right = ParseUnary();
                    left = new GitHubBinaryNode(GitHubBooleanOperator.And, left, right);
                    continue;
                }

                break;
            }

            return left;
        }

        private GitHubQueryNode ParseUnary()
        {
            if (Match(TokenType.Not))
            {
                EnsureOperandAfterOperator("NOT");
                var operand = ParseUnary();
                return new GitHubUnaryNode(GitHubUnaryOperator.Not, operand);
            }

            return ParsePrimary();
        }

        private GitHubQueryNode ParsePrimary()
        {
            if (Match(TokenType.LeftParen))
            {
                if (Peek().Type == TokenType.RightParen)
                    throw Error("Empty parenthesized expression is not allowed.");

                var inner = ParseOr();
                if (!Match(TokenType.RightParen))
                    throw Error("Missing closing ')' in expression.");

                return inner;
            }

            var token = Peek();
            if (token.Type != TokenType.Term)
                throw Error($"Expected a term but found '{token.Display}'.");

            Advance();

            if (TryParseQualifier(token.Value, out var qualifier))
                return new GitHubTermNode(token.Value, token.IsPhrase, qualifier);

            return new GitHubTermNode(token.Value, token.IsPhrase);
        }

        private static bool StartsPrimary(TokenType type) =>
            type is TokenType.Term or TokenType.LeftParen or TokenType.Not;

        private bool Match(TokenType type)
        {
            if (Peek().Type != type)
                return false;

            _idx++;
            return true;
        }

        private void Advance() => _idx++;

        private Token Peek() => _tokens[Math.Min(_idx, _tokens.Count - 1)];

        private void EnsureOperandAfterOperator(string op)
        {
            var t = Peek();
            if (!StartsPrimary(t.Type))
                throw Error($"Operator '{op}' must be followed by a valid term, qualifier, or grouped expression.");
        }

        private GitHubSearchQueryException Error(string message)
        {
            var token = Peek();
            return new GitHubSearchQueryException($"{message} Position={token.Position}.");
        }
    }

    private static class DebugTreeFormatter
    {
        public static string Format(GitHubQueryNode root)
        {
            var lines = new List<string>();
            Walk(root, string.Empty, true, lines);
            return string.Join(Environment.NewLine, lines);
        }

        private static void Walk(GitHubQueryNode node, string indent, bool isLast, List<string> lines)
        {
            var connector = indent.Length == 0 ? string.Empty : (isLast ? "└─" : "├─");
            lines.Add(indent + connector + Describe(node));
            var childIndent = indent + (indent.Length == 0 ? string.Empty : (isLast ? "  " : "│ "));

            switch (node)
            {
                case GitHubBinaryNode bin:
                    Walk(bin.Left, childIndent, false, lines);
                    Walk(bin.Right, childIndent, true, lines);
                    break;
                case GitHubUnaryNode unary:
                    Walk(unary.Operand, childIndent, true, lines);
                    break;
            }
        }

        private static string Describe(GitHubQueryNode node) => node switch
        {
            GitHubBinaryNode b => b.Operator.ToString().ToUpperInvariant(),
            GitHubUnaryNode u => u.Operator.ToString().ToUpperInvariant(),
            GitHubTermNode t when t.Qualifier is not null =>
                $"QUALIFIER({t.Qualifier.Name}:{t.Qualifier.Value})",
            GitHubTermNode t when t.IsPhrase =>
                $"PHRASE(\"{t.Value}\")",
            GitHubTermNode t =>
                $"TERM({t.Value})",
            _ => node.GetType().Name
        };
    }
}
