using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Models;
using System.Text.RegularExpressions;
using System.Text;

namespace CityDiscovery.RecommendationService.Application.Services;

public class VenueFeatureExtractor : IVenueFeatureExtractor
{
    private static readonly Regex HtmlRegex = new Regex("<.*?>", RegexOptions.Compiled);
    private static readonly Regex SpecialCharsRegex = new Regex(@"[^a-z0-9\s,]", RegexOptions.Compiled);

    public string ExtractFeatureText(VenueProfile profile)
    {
        var sb = new StringBuilder();

        // 1. Description
        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            sb.Append(profile.Description);
            sb.Append(' ');
        }

        // 2. Categories
        if (profile.Categories?.Any() == true)
        {
            sb.Append(string.Join(", ", profile.Categories));
            sb.Append(' ');
        }

        // 3. Menu Items
        if (profile.MenuItems?.Any() == true)
        {
            sb.Append(string.Join(" ", profile.MenuItems.Select(m => m.Name)));
            sb.Append(' ');
        }

        // 4. Events
        if (profile.Events?.Any() == true)
        {
            sb.Append(string.Join(" ", profile.Events.Select(e => e.Title)));
            sb.Append(' ');
        }

        var rawText = sb.ToString().Trim();
        if (string.IsNullOrEmpty(rawText)) return string.Empty;

        // Apply Transformation Rules
        return Normalize(rawText);
    }

    private string Normalize(string text)
    {
        // Lowercase
        text = text.ToLowerInvariant();

        // Remove HTML
        text = HtmlRegex.Replace(text, string.Empty);

        // Remove Emojis & Special Characters (keeping only alphanumeric, spaces, and commas for categories)
        text = SpecialCharsRegex.Replace(text, string.Empty);

        // Collapse multiple spaces
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}
