using System.Text.Json;
using MassTransit;

namespace CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;

/// <summary>
/// Helper for deserializing cross-service MassTransit messages.
///
/// When consuming from another service's exchange, messages arrive as
/// application/vnd.masstransit+json envelopes:
///   { "messageType": [...], "message": { ...actual payload... }, ... }
///
/// MassTransit's raw JSON deserializer maps the outer envelope to the DTO,
/// so VenueId etc. are always Guid.Empty. This helper reads the raw bytes,
/// checks if it is an envelope, and extracts the inner "message" object.
/// Falls back to deserializing the root if there is no "message" field.
/// </summary>
public static class MassTransitEnvelopeHelper
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Deserializes the message body from the consume context.
    /// Handles both MassTransit envelope format and plain JSON.
    /// Returns null if deserialization fails.
    /// </summary>
    public static T? Deserialize<T>(ConsumeContext context) where T : class
    {
        // Read raw bytes from the receive context body.
        // ReceiveContext.Body may be read multiple times (backed by byte array in RabbitMQ transport).
        var receiveBody = context.ReceiveContext.Body;
        using var stream = receiveBody.GetStream();

        // Copy to memory in case the stream is forward-only
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var doc = JsonDocument.Parse(ms);
        JsonElement target = doc.RootElement;

        // Unwrap MassTransit envelope: extract inner "message" field if present
        if (target.TryGetProperty("message", out var messageElement) &&
            messageElement.ValueKind == JsonValueKind.Object)
        {
            target = messageElement;
        }

        return JsonSerializer.Deserialize<T>(target.GetRawText(), _options);
    }
}
