using CityDiscovery.RecommendationService.Domain.Entities;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Repository interface for <see cref="VenueEmbedding"/>.
/// </summary>
public interface IVenueEmbeddingRepository
{
    Task<VenueEmbedding?> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VenueEmbedding>> GetAllByCityIdAsync(int cityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VenueEmbedding>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(VenueEmbedding venueEmbedding, CancellationToken cancellationToken = default);
    Task UpdateAsync(VenueEmbedding venueEmbedding, CancellationToken cancellationToken = default);
    Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);
}
