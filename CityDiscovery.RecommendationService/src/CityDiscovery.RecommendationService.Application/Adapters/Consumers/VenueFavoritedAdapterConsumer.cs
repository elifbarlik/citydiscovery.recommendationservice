using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for ReviewService's VenueFavoritedEvent.
/// Maps to the same logic as VenueFavoritedEventHandler.
/// Generates a deterministic EventId for idempotency.
/// </summary>
public class VenueFavoritedAdapterConsumer : IConsumer<ReviewServiceVenueFavoritedDto>
{
    private readonly IInteractionWeightProvider _weightProvider;
    private readonly IInteractionRepository _repository;
    private readonly ITimeDecayService _timeDecayService;
    private readonly ISessionService _sessionService;
    private readonly IRecommendationCacheInvalidator _cacheInvalidator;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<VenueFavoritedAdapterConsumer> _logger;

    public VenueFavoritedAdapterConsumer(
        IInteractionWeightProvider weightProvider,
        IInteractionRepository repository,
        ITimeDecayService timeDecayService,
        ISessionService sessionService,
        IRecommendationCacheInvalidator cacheInvalidator,
        IIdempotencyService idempotency,
        ILogger<VenueFavoritedAdapterConsumer> logger)
    {
        _weightProvider = weightProvider;
        _repository = repository;
        _timeDecayService = timeDecayService;
        _sessionService = sessionService;
        _cacheInvalidator = cacheInvalidator;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReviewServiceVenueFavoritedDto> context)
    {
        var dto = context.Message.VenueId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<ReviewServiceVenueFavoritedDto>(context);

        if (dto is null || dto.VenueId == Guid.Empty)
        {
            _logger.LogError("[Adapter] VenueFavorited could not be deserialized — VenueId is empty.");
            return;
        }

        var eventId = GenerateDeterministicId(dto.UserId, dto.VenueId, dto.FavoritedAt);

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] VenueFavorited '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing VenueFavorited from ReviewService: UserId='{UserId}', VenueId='{VenueId}'",
            dto.UserId, dto.VenueId);

        double baseWeight = _weightProvider.GetWeight("Favorite");
        var sessionId = _sessionService.GetActiveSession(dto.UserId) ?? _sessionService.StartSession(dto.UserId);
        var occurredAt = dto.FavoritedAt != default ? dto.FavoritedAt : DateTime.UtcNow;
        var timeDecayWeight = _timeDecayService.ComputeWeight(baseWeight, occurredAt, sessionId, DateTime.UtcNow, sessionId);

        var log = new InteractionLog
        {
            UserId = dto.UserId,
            VenueId = dto.VenueId,
            SessionId = sessionId,
            InteractionType = "Favorite",
            Weight = baseWeight,
            TimeDecayWeight = timeDecayWeight,
            Timestamp = occurredAt
        };

        await _repository.LogInteractionAsync(log, context.CancellationToken);
        _cacheInvalidator.InvalidateForUser(dto.UserId);
        await _idempotency.RecordAsync(eventId, "ReviewServiceVenueFavoritedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Logged Favorite interaction: User='{UserId}', Venue='{VenueId}', Weight={Weight}",
            dto.UserId, dto.VenueId, baseWeight);
    }

    private static Guid GenerateDeterministicId(Guid userId, Guid venueId, DateTime timestamp)
    {
        var bytes = new byte[16];
        var userBytes = userId.ToByteArray();
        var venueBytes = venueId.ToByteArray();

        for (int i = 0; i < 8; i++)
            bytes[i] = (byte)(userBytes[i] ^ venueBytes[i]);

        var tickBytes = BitConverter.GetBytes(timestamp.Ticks);
        Array.Copy(tickBytes, 0, bytes, 8, 8);

        return new Guid(bytes);
    }
}
