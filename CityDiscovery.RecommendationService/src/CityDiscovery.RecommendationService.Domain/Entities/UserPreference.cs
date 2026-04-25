namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// Stores user category preferences for onboarding / cold-start recommendations.
/// </summary>
public class UserPreference
{
    public Guid UserId { get; set; }
    public List<string> PreferredCategories { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
