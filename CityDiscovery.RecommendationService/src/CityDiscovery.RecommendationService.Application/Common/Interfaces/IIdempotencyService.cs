namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Service for managing event idempotency.
/// Ensures events are processed exactly once.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if an event has already been processed.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <returns>True if the event exists, false otherwise.</returns>
    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an event as successfully processed.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="eventType">The type of the event.</param>
    Task RecordAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default);
}
