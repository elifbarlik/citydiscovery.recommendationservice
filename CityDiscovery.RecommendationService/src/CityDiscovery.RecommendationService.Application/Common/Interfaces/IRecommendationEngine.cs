using CityDiscovery.RecommendationService.Domain.Models;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IRecommendationEngine
{
    Task<RecommendationResult> GetRecommendationsAsync(
        Guid userId,
        int cityId,
        int limit,
        int offset = 0,
        Guid? sessionId = null,
        List<string>? categories = null,
        CancellationToken cancellationToken = default);
}
