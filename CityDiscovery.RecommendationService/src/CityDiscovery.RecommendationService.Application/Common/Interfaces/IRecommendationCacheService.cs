using CityDiscovery.RecommendationService.Domain.Models;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Caches recommendation results with TTL based on session state.
/// </summary>
public interface IRecommendationCacheService
{
    Task<RecommendationResult?> GetOrComputeAsync(
        Guid userId,
        int cityId,
        int limit,
        int offset,
        Guid? sessionId,
        bool hasActiveSession,
        List<string>? categories = null,
        CancellationToken cancellationToken = default);
}
