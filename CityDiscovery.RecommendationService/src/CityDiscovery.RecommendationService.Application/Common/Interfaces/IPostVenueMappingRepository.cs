using CityDiscovery.RecommendationService.Domain.Entities;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IPostVenueMappingRepository
{
    Task<Guid?> GetVenueIdByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task AddAsync(PostVenueMapping mapping, CancellationToken cancellationToken = default);
    Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);
}
