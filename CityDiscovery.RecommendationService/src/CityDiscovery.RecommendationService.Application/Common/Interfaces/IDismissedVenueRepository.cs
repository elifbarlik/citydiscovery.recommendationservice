using CityDiscovery.RecommendationService.Domain.Entities;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IDismissedVenueRepository
{
    Task<IReadOnlyList<Guid>> GetDismissedVenueIdsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DismissAsync(Guid userId, Guid venueId, CancellationToken cancellationToken = default);
    Task UndismissAsync(Guid userId, Guid venueId, CancellationToken cancellationToken = default);
    Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
