using System.Text.RegularExpressions;
using GitHubEmailScraper.Models;

namespace GitHubEmailScraper.Utilities;

/// <summary>
/// Static utilities for validating and extracting email addresses from arbitrary text.
/// All methods are thread-safe.
/// </summary>
public static class EmailValidator
{
    // Practical RFC 5322 subset — captures localpart@domain.tld
    // Negative lookbehind prevents matching URLs like img@2x or hash@sha256
    private static readonly Regex EmailRegex = new(
        @"(?<![=:\/\\\w#])([a-zA-Z0-9][a-zA-Z0-9._%+\-]{0,63}@[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(2));

    private static readonly HashSet<string> FreemailDomains =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "live.com",
            "protonmail.com", "icloud.com", "me.com", "mac.com", "aol.com",
            "yandex.com", "mail.com", "zoho.com", "tutanota.com", "fastmail.com",
            "msn.com", "inbox.com", "gmx.com", "gmx.net", "yahoo.co.uk",
            "yahoo.ca", "googlemail.com", "proton.me", "hey.com", "pm.me"
        };

    private static readonly HashSet<string> SkipDomains =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "users.noreply.github.com",
            "noreply.github.com",
            "privaterelay.appleid.com",
            "example.com",
            "example.org",
            "test.com",
            "localhost",
            "domain.com",
            "email.com"
        };

    // Patterns that reliably indicate non-real / placeholder emails
    private static readonly Regex SuspiciousPattern = new(
        @"(no-?reply|noreply|donotreply|do-not-reply|test@|admin@example|info@example|foo@|bar@|baz@|user@|email@email|sample@|dummy@|placeholder@)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    /// <summary>Returns true if the email passes all validation checks.</summary>
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.Length < 6)
            return false;

        if (!EmailRegex.IsMatch(email))
            return false;

        var domain = GetDomain(email);
        if (domain is null) return false;

        if (SkipDomains.Contains(domain)) return false;

        // Domain must have at least one dot
        if (!domain.Contains('.')) return false;

        if (SuspiciousPattern.IsMatch(email)) return false;

        return true;
    }

    /// <summary>Assigns confidence based on whether the domain is a known freemail provider.</summary>
    public static EmailConfidence Score(string email)
    {
        var domain = GetDomain(email);
        if (domain is null) return EmailConfidence.Low;
        return FreemailDomains.Contains(domain) ? EmailConfidence.Medium : EmailConfidence.High;
    }

    /// <summary>
    /// Extracts all valid, unique (case-insensitive) email addresses from a block of text.
    /// </summary>
    public static IEnumerable<string> ExtractAll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        HashSet<string>? seen = null;

        MatchCollection matches;
        try { matches = EmailRegex.Matches(text); }
        catch (RegexMatchTimeoutException) { yield break; }

        foreach (Match m in matches)
        {
            // Strip trailing dots that sometimes get included
            var raw = m.Groups[1].Value.Trim('.');
            var email = raw.ToLowerInvariant();

            if (!IsValid(email)) continue;

            seen ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (seen.Add(email))
                yield return email;
        }
    }

    /// <summary>Returns the domain part of an email address, or null if malformed.</summary>
    public static string? GetDomain(string? email)
    {
        if (string.IsNullOrEmpty(email)) return null;
        var at = email.LastIndexOf('@');
        return at > 0 && at < email.Length - 1 ? email[(at + 1)..] : null;
    }
}
