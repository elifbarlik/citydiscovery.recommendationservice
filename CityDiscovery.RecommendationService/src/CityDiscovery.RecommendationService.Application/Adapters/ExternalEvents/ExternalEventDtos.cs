using System.Text.Json.Serialization;

// ============================================================================
// DTOs matching the ACTUAL event payloads published by other services.
//
// CRITICAL: Each DTO class MUST be in a namespace that matches the producer's
// CLR type namespace exactly. MassTransit uses the namespace + class name to
// build the messageType URN for matching. If the namespace doesn't match, the
// default deserializer sends messages to the _skipped queue.
//
// Exchange name format: "Namespace:ClassName"
// e.g. "CityDiscovery.VenueService.VenuesService.Shared.Common.Events.Venue:VenueCreatedEvent"
// means namespace = CityDiscovery.VenueService.VenuesService.Shared.Common.Events.Venue
//      class name  = VenueCreatedEvent
//
// We use ClearSerialization() to bypass URN matching entirely, so namespace
// does NOT need to match. But [JsonPropertyName] attributes ARE required to map
// camelCase JSON keys from the MassTransit envelope's "message" field.
// ============================================================================

namespace CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;

// ---------------------------------------------------------------------------
// FROM: VenueService
// Exchange: CityDiscovery.VenueService.VenuesService.Shared.Common.Events.Venue:VenueCreatedEvent
// ---------------------------------------------------------------------------
public class VenueServiceVenueCreatedDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("occurredOn")]
    public DateTime OccurredOn { get; set; }

    [JsonPropertyName("venueId")]
    public Guid VenueId { get; set; }

    [JsonPropertyName("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: VenueService
// Exchange: CityDiscovery.VenuesService.Shared.Common.Events.Venue:VenueUpdatedEvent
// NOTE: different namespace than VenueCreatedEvent!
// ---------------------------------------------------------------------------
public class VenueServiceVenueUpdatedDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("occurredOn")]
    public DateTime OccurredOn { get; set; }

    [JsonPropertyName("venueId")]
    public Guid VenueId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("cityId")]
    public int? CityId { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: SocialService — PostLikedEvent
// CRITICAL: Does NOT contain VenueId
// ---------------------------------------------------------------------------
public class SocialServicePostLikedDto
{
    [JsonPropertyName("postId")]
    public Guid PostId { get; set; }

    [JsonPropertyName("postAuthorUserId")]
    public Guid PostAuthorUserId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("likedAt")]
    public DateTime LikedAt { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: ReviewService — ReviewCreatedEvent
// ---------------------------------------------------------------------------
public class ReviewServiceReviewCreatedDto
{
    [JsonPropertyName("reviewId")]
    public Guid ReviewId { get; set; }

    [JsonPropertyName("venueId")]
    public Guid VenueId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonPropertyName("venueOwnerId")]
    public Guid VenueOwnerId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: ReviewService — VenueFavoritedEvent
// ---------------------------------------------------------------------------
public class ReviewServiceVenueFavoritedDto
{
    [JsonPropertyName("venueId")]
    public Guid VenueId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [JsonPropertyName("favoritedAt")]
    public DateTime FavoritedAt { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: SocialService — PostCreatedEvent
// Contains VenueId — used to build PostId→VenueId mapping for PostLiked lookup.
// ---------------------------------------------------------------------------
public class SocialServicePostCreatedDto
{
    [JsonPropertyName("postId")]
    public Guid PostId { get; set; }

    [JsonPropertyName("venueId")]
    public Guid VenueId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: VenueService — VenueDeletedEvent
// ---------------------------------------------------------------------------
public class VenueServiceVenueDeletedDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("occurredOn")]
    public DateTime OccurredOn { get; set; }

    [JsonPropertyName("venueId")]
    public Guid VenueId { get; set; }

    [JsonPropertyName("venueName")]
    public string VenueName { get; set; } = string.Empty;

    [JsonPropertyName("deletedAt")]
    public DateTime DeletedAt { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: SocialService — PostSavedEvent
// ---------------------------------------------------------------------------
public class SocialServicePostSavedDto
{
    [JsonPropertyName("postId")]
    public Guid PostId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("savedAt")]
    public DateTime SavedAt { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: IdentityService — UserDeletedEvent
// ---------------------------------------------------------------------------
public class IdentityServiceUserDeletedDto
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("deletedAtUtc")]
    public DateTime DeletedAtUtc { get; set; }
}

// ---------------------------------------------------------------------------
// FROM: IdentityService — UserLoggedInEvent
// ---------------------------------------------------------------------------
public class IdentityServiceUserLoggedInDto
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("loggedInAtUtc")]
    public DateTime LoggedInAtUtc { get; set; }
}
