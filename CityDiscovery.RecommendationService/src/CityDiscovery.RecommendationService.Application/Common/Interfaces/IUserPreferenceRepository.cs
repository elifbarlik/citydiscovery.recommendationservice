using CityDiscovery.RecommendationService.Domain.Entities;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(UserPreference preference, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
