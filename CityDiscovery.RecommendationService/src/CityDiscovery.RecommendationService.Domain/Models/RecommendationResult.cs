namespace CityDiscovery.RecommendationService.Domain.Models;

public record RecommendationResult(
    IReadOnlyList<ScoredVenue> Venues,
    string Strategy);
