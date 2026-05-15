using GitHubEmailScraper.Models;
using GitHubEmailScraper.Utilities;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Returns the email address that the developer has publicly set on their GitHub profile
/// (the API <c>email</c> field). Users without a public email are skipped.
/// </summary>
public sealed class EmailExtractionService : IEmailExtractionService
{
    private readonly PipelineStats _stats;
    private readonly ILogger<EmailExtractionService> _logger;

    public EmailExtractionService(
        PipelineStats stats,
        ILogger<EmailExtractionService> logger)
    {
        _stats = stats;
        _logger = logger;
    }

    public Task<IReadOnlyList<EmailResult>> ExtractAsync(
        GitHubUser user, CancellationToken ct = default)
    {
        var found = new List<EmailResult>();

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var email = user.Email.Trim().ToLowerInvariant();
            if (EmailValidator.IsValid(email))
            {
                found.Add(new EmailResult
                {
                    Email      = email,
                    Source     = "ApiField",
                    Confidence = EmailValidator.Score(email),
                    IsValid    = true
                });
                _stats.IncrementEmailsFound();
                _logger.LogDebug("Email found for {Username}: {Email}", user.Login, email);
            }
        }

        return Task.FromResult<IReadOnlyList<EmailResult>>(found);
    }
}
