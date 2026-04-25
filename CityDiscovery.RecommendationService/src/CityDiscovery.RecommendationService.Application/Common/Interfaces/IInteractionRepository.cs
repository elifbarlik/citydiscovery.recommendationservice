using CityDiscovery.RecommendationService.Domain.Entities;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IInteractionRepository
{
    Task LogInteractionAsync(InteractionLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all interaction logs for a user.
    /// </summary>
    Task<IReadOnlyList<InteractionLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all venue IDs the user has interacted with (for exclusion in recommendations).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetInteractedVenueIdsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets venue IDs ordered by interaction count (for popularity fallback).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetMostInteractedVenueIdsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets interaction counts per venue for the given venue IDs (for popularity scoring).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetInteractionCountsByVenueIdsAsync(IEnumerable<Guid> venueIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent interaction timestamp per venue.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTime>> GetLastInteractionDatesByVenueIdsAsync(IEnumerable<Guid> venueIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets venue IDs the user interacted with in the given session.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetVenueIdsBySessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most interacted venue IDs in a city within the last N days (for trending).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetTrendingVenueIdsAsync(int cityId, int days, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all interaction logs for a given venue (called when venue is deleted).
    /// </summary>
    Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all interaction logs for a given user (called when user is deleted).
    /// </summary>
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
