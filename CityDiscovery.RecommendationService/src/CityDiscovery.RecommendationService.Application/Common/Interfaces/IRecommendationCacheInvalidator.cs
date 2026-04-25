namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Invalidates recommendation cache when user interactions change.
/// </summary>
public interface IRecommendationCacheInvalidator
{
    void InvalidateForUser(Guid userId);
}
