namespace CityDiscovery.RecommendationService.Domain.Models;

public record ScoredVenue(
    Guid VenueId,
    double Score,
    string Strategy = "embedding_similarity",
    bool SessionInfluenced = false);
