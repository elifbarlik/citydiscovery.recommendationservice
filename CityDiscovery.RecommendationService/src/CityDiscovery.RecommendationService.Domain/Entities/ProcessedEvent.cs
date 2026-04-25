namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// Represents an integration event that has been successfully processed.
/// Used for idempotency checks to prevent duplicate processing.
/// </summary>
public class ProcessedEvent
{
    /// <summary>
    /// The unique identifier of the event.
    /// This is the primary key and must be unique.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// The type of the event (e.g., "venue.created").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// When the event was processed by this service.
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
