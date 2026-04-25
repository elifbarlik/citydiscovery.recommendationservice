namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// Represents a logged user-venue interaction for the Feature Store.
/// Used to train recommendation models.
/// </summary>
public class InteractionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    public Guid VenueId { get; set; }
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// Type of interaction: Like, Save, Favorite, Review.
    /// </summary>
    public string InteractionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Calculated weight of the interaction for scoring.
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// Weight used for time decay calculations in online learning.
    /// </summary>
    public double TimeDecayWeight { get; set; } = 1.0;
    
    public DateTime Timestamp { get; set; }
}
