using System;

namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// Stores the vector representation of a venue for semantic similarity search.
/// </summary>
public class VenueEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    /// <summary>
    /// City ID for filtering recommendations by city. Matches VenueService's int CityId. Null if unknown.
    /// </summary>
    public int? CityId { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    /// <summary>
    /// Venue categories (e.g. Restaurant, Cafe) from VenueCreatedEvent.
    /// </summary>
    public List<string> Categories { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
