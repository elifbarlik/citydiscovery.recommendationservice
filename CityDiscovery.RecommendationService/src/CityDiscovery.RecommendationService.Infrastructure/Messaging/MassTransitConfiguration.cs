using CityDiscovery.RecommendationService.Application.Consumers;
using CityDiscovery.Shared.Events.Review;
using CityDiscovery.Shared.Events.UserInteractions;
using CityDiscovery.Shared.Events.Venue;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityDiscovery.RecommendationService.Infrastructure.Messaging;

/// <summary>
/// Configures MassTransit with RabbitMQ transport, consumers, retry policies, and dead-letter queues.
/// </summary>
public static class MassTransitConfiguration
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ and all event consumers.
    /// Queue naming convention: recommendation.{event-name}
    /// DLQ naming convention:   recommendation.{event-name}_error
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
            // --- Register consumers ---
            x.AddConsumer<VenueCreatedConsumer>();
            x.AddConsumer<VenueUpdatedConsumer>();
            x.AddConsumer<PostLikedConsumer>();
            x.AddConsumer<PostSavedConsumer>();
            x.AddConsumer<VenueFavoritedConsumer>();
            x.AddConsumer<ReviewSubmittedConsumer>();
            x.AddConsumer<VenueViewedConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(new Uri(host), h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.UseRawJsonDeserializer();

                // --- VenueCreated ---
                cfg.ReceiveEndpoint("recommendation.venue-created", e =>
                {
                    ConfigureEndpoint<VenueCreatedConsumer, VenueCreatedEvent>(ctx, e);
                });

                // --- VenueUpdated ---
                cfg.ReceiveEndpoint("recommendation.venue-updated", e =>
                {
                    ConfigureEndpoint<VenueUpdatedConsumer, VenueUpdatedEvent>(ctx, e);
                });

                // --- PostLiked ---
                cfg.ReceiveEndpoint("recommendation.post-liked", e =>
                {
                    ConfigureEndpoint<PostLikedConsumer, PostLikedEvent>(ctx, e);
                });

                // --- PostSaved ---
                cfg.ReceiveEndpoint("recommendation.post-saved", e =>
                {
                    ConfigureEndpoint<PostSavedConsumer, PostSavedEvent>(ctx, e);
                });

                // --- VenueFavorited ---
                cfg.ReceiveEndpoint("recommendation.venue-favorited", e =>
                {
                    ConfigureEndpoint<VenueFavoritedConsumer, VenueFavoritedEvent>(ctx, e);
                });

                // --- ReviewSubmitted ---
                cfg.ReceiveEndpoint("recommendation.review-submitted", e =>
                {
                    ConfigureEndpoint<ReviewSubmittedConsumer, ReviewSubmitted>(ctx, e);
                });

                // --- VenueViewed (user interaction) ---
                cfg.ReceiveEndpoint("recommendation.venue-viewed", e =>
                {
                    ConfigureEndpoint<VenueViewedConsumer, VenueViewedEvent>(ctx, e);
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Shared endpoint configuration: retry policy and consumer wiring.
    /// MassTransit by default moves failed messages to {queue_name}_error after retries.
    /// </summary>
    private static void ConfigureEndpoint<TConsumer, TMessage>(
        IBusRegistrationContext ctx,
        IRabbitMqReceiveEndpointConfigurator e)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        // Retry: 3 attempts with increasing intervals (1s, 5s, 30s)
        e.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30)));

        // Configure the consumer
        e.ConfigureConsumer<TConsumer>(ctx);
    }
}

