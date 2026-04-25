using CityDiscovery.RecommendationService.Application.Adapters.Consumers;
using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;

using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityDiscovery.RecommendationService.Infrastructure.Messaging;

/// <summary>
/// Configures MassTransit with RabbitMQ transport, consumers, retry policies, and dead-letter queues.
/// 
/// TWO SETS OF CONSUMERS:
/// 1. "Shared" consumers — listen on CityDiscovery.Shared exchange names (future-proof)
/// 2. "Adapter" consumers — listen on actual service exchanges via Bind() (current compatibility)
/// </summary>
public static class MassTransitConfiguration
{
    // -----------------------------------------------------------------------
    // Exchange names used by each producer service (derived from their CLR types).
    // These MUST match the full namespace:ClassName of the publishing service's event class.
    // -----------------------------------------------------------------------
    private const string VenueService_VenueCreated =
        "CityDiscovery.VenueService.VenuesService.Shared.Common.Events.Venue:VenueCreatedEvent";
    private const string VenueService_VenueUpdated =
        "CityDiscovery.VenuesService.Shared.Common.Events.Venue:VenueUpdatedEvent";
    private const string VenueService_VenueDeleted =
        "CityDiscovery.VenueService.VenuesService.Shared.Common.Events.Venue:VenueDeletedEvent";
    private const string SocialService_PostCreated =
        "CityDiscovery.SocialService.SocialServiceShared.Common.Events.Social:PostCreatedEvent";
    private const string SocialService_PostLiked =
        "CityDiscovery.SocialService.SocialServiceShared.Common.Events.Social:PostLikedEvent";
    private const string SocialService_PostSaved =
        "CityDiscovery.SocialService.SocialServiceShared.Common.Events.Social:PostSavedEvent";
    private const string ReviewService_ReviewCreated =
        "CityDiscovery.ReviewService.ReviewService.Shared.Events.Review:ReviewCreatedEvent";
    private const string ReviewService_VenueFavorited =
        "CityDiscovery.ReviewService.ReviewService.Shared.Events.Venue:VenueFavoritedEvent";
    private const string IdentityService_UserDeleted =
        "IdentityService.Shared.MessageBus.Identity:UserDeletedEvent";
    private const string IdentityService_UserLoggedIn =
        "IdentityService.Shared.MessageBus.Identity:UserLoggedInEvent";

    /// <summary>
    /// Registers MassTransit with RabbitMQ and all event consumers.
    /// </summary>
    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var host     = configuration["RabbitMQ:Host"]     ?? "rabbitmq://localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            // --- Register Adapter consumers (for current service compatibility) ---
            x.AddConsumer<VenueCreatedAdapterConsumer>();
            x.AddConsumer<VenueUpdatedAdapterConsumer>();
            x.AddConsumer<VenueDeletedAdapterConsumer>();
            x.AddConsumer<PostCreatedAdapterConsumer>();
            x.AddConsumer<PostLikedAdapterConsumer>();
            x.AddConsumer<ReviewCreatedAdapterConsumer>();
            x.AddConsumer<VenueFavoritedAdapterConsumer>();
            x.AddConsumer<UserDeletedAdapterConsumer>();
            x.AddConsumer<UserLoggedInAdapterConsumer>();
            x.AddConsumer<PostSavedAdapterConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(new Uri(host), h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // ===============================================================
                // ADAPTER CONSUMERS — bind to ACTUAL service exchanges via Bind()
                // These consume the real events published by other services NOW.
                //
                // Each endpoint uses ClearSerialization() + UseRawJsonSerializer()
                // to remove the standard MassTransit deserializer, so messages
                // with content-type application/vnd.masstransit+json (envelope)
                // and application/json (raw) are both handled by the raw
                // deserializer which bypasses messageType URN matching.
                // ===============================================================

                // VenueService → VenueCreated
                cfg.ReceiveEndpoint("recommendation.adapter.venue-created", e =>
                {
                    e.Bind(VenueService_VenueCreated);
                    ConfigureEndpoint<VenueCreatedAdapterConsumer, VenueServiceVenueCreatedDto>(ctx, e);
                });

                // VenueService → VenueUpdated (different namespace than VenueCreated!)
                cfg.ReceiveEndpoint("recommendation.adapter.venue-updated", e =>
                {
                    e.Bind(VenueService_VenueUpdated);
                    ConfigureEndpoint<VenueUpdatedAdapterConsumer, VenueServiceVenueUpdatedDto>(ctx, e);
                });

                // SocialService → PostLiked (VenueId missing — logs warning)
                cfg.ReceiveEndpoint("recommendation.adapter.post-liked", e =>
                {
                    e.Bind(SocialService_PostLiked);
                    ConfigureEndpoint<PostLikedAdapterConsumer, SocialServicePostLikedDto>(ctx, e);
                });

                // ReviewService → ReviewCreated (maps to ReviewSubmitted logic)
                cfg.ReceiveEndpoint("recommendation.adapter.review-created", e =>
                {
                    e.Bind(ReviewService_ReviewCreated);
                    ConfigureEndpoint<ReviewCreatedAdapterConsumer, ReviewServiceReviewCreatedDto>(ctx, e);
                });

                // ReviewService → VenueFavorited
                cfg.ReceiveEndpoint("recommendation.adapter.venue-favorited", e =>
                {
                    e.Bind(ReviewService_VenueFavorited);
                    ConfigureEndpoint<VenueFavoritedAdapterConsumer, ReviewServiceVenueFavoritedDto>(ctx, e);
                });

                // VenueService → VenueDeleted
                cfg.ReceiveEndpoint("recommendation.adapter.venue-deleted", e =>
                {
                    e.Bind(VenueService_VenueDeleted);
                    ConfigureEndpoint<VenueDeletedAdapterConsumer, VenueServiceVenueDeletedDto>(ctx, e);
                });

                // SocialService → PostCreated (also builds PostId→VenueId mapping)
                cfg.ReceiveEndpoint("recommendation.adapter.post-created", e =>
                {
                    e.Bind(SocialService_PostCreated);
                    ConfigureEndpoint<PostCreatedAdapterConsumer, SocialServicePostCreatedDto>(ctx, e);
                });

                // IdentityService → UserDeleted (GDPR cleanup)
                cfg.ReceiveEndpoint("recommendation.adapter.user-deleted", e =>
                {
                    e.Bind(IdentityService_UserDeleted);
                    ConfigureEndpoint<UserDeletedAdapterConsumer, IdentityServiceUserDeletedDto>(ctx, e);
                });

                // IdentityService → UserLoggedIn (session start)
                cfg.ReceiveEndpoint("recommendation.adapter.user-logged-in", e =>
                {
                    e.Bind(IdentityService_UserLoggedIn);
                    ConfigureEndpoint<UserLoggedInAdapterConsumer, IdentityServiceUserLoggedInDto>(ctx, e);
                });

                // SocialService → PostSaved (save interaction, VenueId lookup via PostVenueMapping)
                cfg.ReceiveEndpoint("recommendation.adapter.post-saved", e =>
                {
                    e.Bind(SocialService_PostSaved);
                    ConfigureEndpoint<PostSavedAdapterConsumer, SocialServicePostSavedDto>(ctx, e);
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Shared endpoint configuration: serialization, retry policy, and consumer wiring.
    ///
    /// Key settings:
    /// - ConfigureConsumeTopology = false: prevents MassTransit from auto-creating exchanges
    ///   based on the DTO CLR type name. We bind manually via Bind() instead.
    /// - ClearSerialization(): removes default deserializer that enforces messageType URN
    ///   matching. Without this, producer's URN (VenueCreatedEvent) never matches our DTO
    ///   type name (VenueServiceVenueCreatedDto) → message goes to _skipped queue.
    /// - UseRawJsonSerializer(AnyMessageType, isDefault: true): registers as default serializer
    ///   AND correctly unwraps application/vnd.masstransit+json envelopes, extracting the
    ///   inner "message" field into the DTO. The isDefault:true is critical — without it the
    ///   envelope is not unwrapped and all Guid fields become Guid.Empty.
    /// - UseRawJsonDeserializer(AnyMessageType): additional handler for plain application/json.
    /// - All DTO properties carry [JsonPropertyName] attributes to map camelCase JSON
    ///   keys (from producer) to PascalCase C# properties (prevents Guid.Empty / null).
    /// </summary>
    private static void ConfigureEndpoint<TConsumer, TMessage>(
        IBusRegistrationContext ctx,
        IRabbitMqReceiveEndpointConfigurator e)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        // Don't auto-create exchanges based on DTO type names — we use Bind() explicitly
        e.ConfigureConsumeTopology = false;

        // Cross-service message consumption: producer's messageType URN (e.g. VenueCreatedEvent)
        // never matches our local DTO name → messages go to _skipped without ClearSerialization().
        // ClearSerialization() + UseRawJsonSerializer bypasses URN matching so the consumer fires.
        //
        // NOTE: The raw deserializer does NOT unwrap the MassTransit envelope "message" field —
        // it maps the outer envelope to the DTO, resulting in Guid.Empty for all Guid fields.
        // Each consumer handles this via MassTransitEnvelopeHelper.Deserialize<T>() which reads
        // the raw body and extracts the inner "message" object manually.
        e.ClearSerialization();
        e.UseRawJsonSerializer(RawSerializerOptions.AnyMessageType);

        // Retry: 3 attempts with increasing intervals (1s, 5s, 30s)
        e.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30)));

        // Configure the consumer
        e.ConfigureConsumer<TConsumer>(ctx);
    }
}
