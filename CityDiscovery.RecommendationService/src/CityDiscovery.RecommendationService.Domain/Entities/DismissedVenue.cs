namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// Tracks venues that a user has dismissed from their recommendations.
/// </summary>
public class DismissedVenue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VenueId { get; set; }
    public DateTime DismissedAt { get; set; } = DateTime.UtcNow;
}
