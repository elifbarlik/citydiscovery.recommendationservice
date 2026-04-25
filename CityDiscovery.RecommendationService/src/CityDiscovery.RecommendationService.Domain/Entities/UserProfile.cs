using System;

namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// Cached user embedding profile for recommendations.
/// </summary>
public class UserProfile
{
    public Guid UserId { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime LastUpdatedAt { get; set; }
    public Guid? ActiveSessionId { get; set; }
}
