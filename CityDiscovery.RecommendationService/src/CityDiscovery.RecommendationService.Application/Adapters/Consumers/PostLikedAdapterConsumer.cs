using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for SocialService's PostLikedEvent.
///
/// PostLikedEvent'te VenueId bulunmaz. VenueId'yi PostVenueMappings tablosundan
/// PostId üzerinden lookup ederiz. PostCreatedAdapterConsumer bu tabloyu doldurur.
/// Mapping bulunamazsa event loglanır ve atlanır.
/// </summary>
public class PostLikedAdapterConsumer : IConsumer<SocialServicePostLikedDto>
{
    private readonly IPostVenueMappingRepository _mappingRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IInteractionWeightProvider _weightProvider;
    private readonly ITimeDecayService _timeDecayService;
    private readonly ISessionService _sessionService;
    private readonly IRecommendationCacheInvalidator _cacheInvalidator;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<PostLikedAdapterConsumer> _logger;

    public PostLikedAdapterConsumer(
        IPostVenueMappingRepository mappingRepository,
        IInteractionRepository interactionRepository,
        IInteractionWeightProvider weightProvider,
        ITimeDecayService timeDecayService,
        ISessionService sessionService,
        IRecommendationCacheInvalidator cacheInvalidator,
        IIdempotencyService idempotency,
        ILogger<PostLikedAdapterConsumer> logger)
    {
        _mappingRepository = mappingRepository;
        _interactionRepository = interactionRepository;
        _weightProvider = weightProvider;
        _timeDecayService = timeDecayService;
        _sessionService = sessionService;
        _cacheInvalidator = cacheInvalidator;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SocialServicePostLikedDto> context)
    {
        var dto = context.Message.PostId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<SocialServicePostLikedDto>(context);

        if (dto is null || dto.PostId == Guid.Empty)
        {
            _logger.LogError("[Adapter] PostLiked could not be deserialized — PostId is empty.");
            return;
        }

        // Deterministic event ID: UserId + PostId + LikedAt
        var eventId = GenerateDeterministicId(dto.UserId, dto.PostId, dto.LikedAt);

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] PostLiked '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        // PostId→VenueId lookup
        var venueId = await _mappingRepository.GetVenueIdByPostIdAsync(dto.PostId, context.CancellationToken);

        if (venueId is null)
        {
            _logger.LogWarning(
                "[Adapter] PostLiked received but PostId='{PostId}' has no VenueId mapping. " +
                "PostCreatedEvent may not have been received yet. UserId='{UserId}'. Skipping.",
                dto.PostId, dto.UserId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing PostLiked: PostId='{PostId}', VenueId='{VenueId}', UserId='{UserId}'",
            dto.PostId, venueId, dto.UserId);

        double baseWeight = _weightProvider.GetWeight("Like");
        var sessionId = _sessionService.GetActiveSession(dto.UserId) ?? _sessionService.StartSession(dto.UserId);
        var occurredAt = dto.LikedAt != default ? dto.LikedAt : DateTime.UtcNow;
        var timeDecayWeight = _timeDecayService.ComputeWeight(baseWeight, occurredAt, sessionId, DateTime.UtcNow, sessionId);

        var log = new InteractionLog
        {
            UserId = dto.UserId,
            VenueId = venueId.Value,
            SessionId = sessionId,
            InteractionType = "Like",
            Weight = baseWeight,
            TimeDecayWeight = timeDecayWeight,
            Timestamp = occurredAt
        };

        await _interactionRepository.LogInteractionAsync(log, context.CancellationToken);
        _cacheInvalidator.InvalidateForUser(dto.UserId);
        await _idempotency.RecordAsync(eventId, "SocialServicePostLikedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Logged Like interaction: User='{UserId}', Venue='{VenueId}', Weight={Weight}",
            dto.UserId, venueId, baseWeight);
    }

    private static Guid GenerateDeterministicId(Guid userId, Guid postId, DateTime timestamp)
    {
        var bytes = new byte[16];
        var userBytes = userId.ToByteArray();
        var postBytes = postId.ToByteArray();

        for (int i = 0; i < 8; i++)
            bytes[i] = (byte)(userBytes[i] ^ postBytes[i]);

        var tickBytes = BitConverter.GetBytes(timestamp.Ticks);
        Array.Copy(tickBytes, 0, bytes, 8, 8);

        return new Guid(bytes);
    }
}
