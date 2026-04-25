namespace CityDiscovery.RecommendationService.Api.Models;

public record RecommendationResponse(
    Guid UserId,
    int CityId,
    Guid SessionId,
    IReadOnlyList<VenueRecommendationDto> Venues,
    DateTime GeneratedAt,
    string Strategy,
    int TotalCount);

public record VenueRecommendationDto(
    Guid VenueId,
    double Score,
    string Strategy,
    bool SessionInfluenced);
