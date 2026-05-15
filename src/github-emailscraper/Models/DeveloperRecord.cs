using CsvHelper.Configuration.Attributes;

namespace GitHubEmailScraper.Models;

public sealed class DeveloperRecord
{
    [Name("Username")]
    public string Username { get; set; } = string.Empty;

    [Name("Name")]
    public string Name { get; set; } = string.Empty;

    [Name("Location")]
    public string Location { get; set; } = string.Empty;

    [Name("Bio")]
    public string Bio { get; set; } = string.Empty;

    [Name("Email")]
    public string Email { get; set; } = string.Empty;

    [Name("EmailSource")]
    public string EmailSource { get; set; } = string.Empty;

    [Name("EmailConfidence")]
    public string EmailConfidence { get; set; } = string.Empty;

    [Name("Website")]
    public string Website { get; set; } = string.Empty;

    [Name("Followers")]
    public int Followers { get; set; }

    [Name("Repos")]
    public int Repos { get; set; }

    [Name("ProfileUrl")]
    public string ProfileUrl { get; set; } = string.Empty;

    [Name("FoundAtUtc")]
    public string FoundAtUtc { get; set; } = string.Empty;
}
