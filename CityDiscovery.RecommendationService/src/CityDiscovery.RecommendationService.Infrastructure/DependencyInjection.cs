using CityDiscovery.RecommendationService.Application.Options;
using CityDiscovery.RecommendationService.Application.Adapters.Consumers;
using CityDiscovery.RecommendationService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Infrastructure.ExternalServices;
using CityDiscovery.RecommendationService.Infrastructure.Messaging;
using CityDiscovery.RecommendationService.Infrastructure.Persistence;
using CityDiscovery.RecommendationService.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.RateLimiting;
using Polly;

namespace CityDiscovery.RecommendationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool addMessaging = true)
    {
        // Persistence
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' yapılandırılmamış. .env dosyasını kontrol edin.");
        services.AddDbContext<RecommendationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMemoryCache();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IInteractionRepository, InteractionRepository>();
        services.AddScoped<IVenueEmbeddingRepository, VenueEmbeddingRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IDismissedVenueRepository, DismissedVenueRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddScoped<IPostVenueMappingRepository, PostVenueMappingRepository>();

        // Embedding Configuration
        var embeddingOptions = configuration.GetSection("EmbeddingProvider").Get<EmbeddingProviderOptions>() 
                             ?? new EmbeddingProviderOptions();
        services.Configure<EmbeddingProviderOptions>(configuration.GetSection("EmbeddingProvider"));
        services.Configure<TimeDecayOptions>(configuration.GetSection(TimeDecayOptions.SectionName));
        services.Configure<SessionServiceOptions>(configuration.GetSection(SessionServiceOptions.SectionName));
        services.Configure<RecommendationWeightsOptions>(configuration.GetSection(RecommendationWeightsOptions.SectionName));

        // VenueService HTTP Client
        var venueServiceUrl = configuration["ExternalServices:VenueServiceUrl"]
                           ?? "http://venue-service:80/";
        services.AddHttpClient<IVenueServiceClient, VenueServiceClient>(client =>
        {
            client.BaseAddress = new Uri(venueServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddResilienceHandler("venue-service-resilience", builder =>
        {
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromSeconds(1)
            });
            builder.AddTimeout(TimeSpan.FromSeconds(5));
        });

        // Gemini HttpClient with Resilience
        services.AddHttpClient("Gemini", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddResilienceHandler("gemini-resilience", builder =>
        {
            // 3 Retries with exponential backoff (1s, 2s, 4s)
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1)
            });

            // Timeout: 10 seconds per request
            builder.AddTimeout(TimeSpan.FromSeconds(10));

            // RateLimiter: max 3 requests per second
            builder.AddRateLimiter(new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(1),
                PermitLimit = 3,
                QueueLimit = 10
            }));
        });

        if (embeddingOptions.UseMock)
        {
            services.AddSingleton<IEmbeddingProvider, MockEmbeddingProvider>();
        }
        else
        {
            services.AddSingleton<IEmbeddingProvider, GeminiEmbeddingProvider>();
        }

        // Domain/Application Services
        services.AddScoped<IInteractionWeightProvider, CityDiscovery.RecommendationService.Application.Services.InteractionWeightProvider>();
        services.AddScoped<ITimeDecayService, CityDiscovery.RecommendationService.Application.Services.TimeDecayService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddScoped<IVenueFeatureExtractor, CityDiscovery.RecommendationService.Application.Services.VenueFeatureExtractor>();
        services.AddScoped<IEmbeddingService, CityDiscovery.RecommendationService.Application.Services.EmbeddingService>();
        services.AddScoped<IUserProfileService, CityDiscovery.RecommendationService.Application.Services.UserProfileService>();
        services.AddScoped<IHybridScoringService, CityDiscovery.RecommendationService.Application.Services.HybridScoringService>();
        services.AddScoped<IRecommendationEngine, CityDiscovery.RecommendationService.Application.Services.CosineSimilarityRecommendationEngine>();
        services.AddSingleton<RecommendationCacheService>();
        services.AddSingleton<IRecommendationCacheService>(sp => sp.GetRequiredService<RecommendationCacheService>());
        services.AddSingleton<IRecommendationCacheInvalidator>(sp => sp.GetRequiredService<RecommendationCacheService>());

        if (addMessaging)
        {
            // Adapter Consumers (for consuming events from external services' native formats)
            services.AddScoped<VenueCreatedAdapterConsumer>();
            services.AddScoped<VenueUpdatedAdapterConsumer>();
            services.AddScoped<VenueDeletedAdapterConsumer>();
            services.AddScoped<PostCreatedAdapterConsumer>();
            services.AddScoped<PostLikedAdapterConsumer>();
            services.AddScoped<ReviewCreatedAdapterConsumer>();
            services.AddScoped<VenueFavoritedAdapterConsumer>();
            services.AddScoped<UserDeletedAdapterConsumer>();
            services.AddScoped<UserLoggedInAdapterConsumer>();
            services.AddScoped<PostSavedAdapterConsumer>();

            // MassTransit + RabbitMQ
            services.AddMassTransitWithRabbitMq(configuration);
        }

        return services;
    }
}

