using System.ComponentModel.DataAnnotations;

namespace GitHubScraper.ViewModels.Operations;

public sealed class CreateOperationViewModel
{
    [Required(ErrorMessage = "Operation name is required.")]
    [MaxLength(200)]
    [Display(Name = "Operation Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hidden field — comma-separated list serialised from the tag input.</summary>
    public string KeywordsRaw { get; set; } = string.Empty;

    /// <summary>Hidden field — comma-separated list serialised from the tag input.</summary>
    public string LocationsRaw { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Min Followers must be 0 or greater.")]
    [Display(Name = "Minimum Followers")]
    public int MinFollowers { get; set; } = 0;

    [Display(Name = "GitHub Personal Access Token (optional)")]
    [MaxLength(500)]
    public string? GitHubToken { get; set; }

    /// <summary>If true, Start the operation immediately after creation.</summary>
    public bool StartImmediately { get; set; } = false;

    // ── Helpers ────────────────────────────────────────────────────────────────

    public List<string> GetKeywords() =>
        SplitRaw(KeywordsRaw);

    public List<string> GetLocations() =>
        SplitRaw(LocationsRaw);

    private static List<string> SplitRaw(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Distinct(StringComparer.OrdinalIgnoreCase)
           .ToList();
}
