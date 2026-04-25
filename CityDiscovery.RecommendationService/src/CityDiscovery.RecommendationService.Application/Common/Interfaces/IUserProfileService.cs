using CityDiscovery.RecommendationService.Domain.Entities;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Computes and persists user embedding profiles using time-decayed interaction weights.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Computes the user's embedding from all interactions (with real-time time decay)
    /// and saves/updates the UserProfile.
    /// </summary>
    Task<UserProfile> ComputeAndSaveUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's current embedding vector (from cache or computed on-the-fly).
    /// Returns null if user has no interactions.
    /// </summary>
    Task<float[]?> GetUserEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default);
}
