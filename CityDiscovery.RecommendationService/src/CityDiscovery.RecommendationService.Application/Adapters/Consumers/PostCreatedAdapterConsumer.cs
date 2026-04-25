using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for SocialService's PostCreatedEvent.
///
/// İki iş yapar:
/// 1. PostId→VenueId eşlemesini PostVenueMappings tablosuna kaydeder.
///    Bu sayede PostLikedEvent geldiğinde hangi mekana ait olduğunu öğrenebiliriz.
/// 2. Post oluşturma etkileşimini InteractionLog'a "Post" tipiyle kaydeder.
/// </summary>
public class PostCreatedAdapterConsumer : IConsumer<SocialServicePostCreatedDto>
{
    private readonly IPostVenueMappingRepository _mappingRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IInteractionWeightProvider _weightProvider;
    private readonly ITimeDecayService _timeDecayService;
    private readonly ISessionService _sessionService;
    private readonly IRecommendationCacheInvalidator _cacheInvalidator;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<PostCreatedAdapterConsumer> _logger;

    public PostCreatedAdapterConsumer(
        IPostVenueMappingRepository mappingRepository,
        IInteractionRepository interactionRepository,
        IInteractionWeightProvider weightProvider,
        ITimeDecayService timeDecayService,
        ISessionService sessionService,
        IRecommendationCacheInvalidator cacheInvalidator,
        IIdempotencyService idempotency,
        ILogger<PostCreatedAdapterConsumer> logger)
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

    public async Task Consume(ConsumeContext<SocialServicePostCreatedDto> context)
    {
        var dto = context.Message.PostId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<SocialServicePostCreatedDto>(context);

        if (dto is null || dto.PostId == Guid.Empty)
        {
            _logger.LogError("[Adapter] PostCreated could not be deserialized — PostId is empty.");
            return;
        }

        var eventId = dto.PostId;

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] PostCreated '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing PostCreated from SocialService: PostId='{PostId}', VenueId='{VenueId}', UserId='{UserId}'",
            dto.PostId, dto.VenueId, dto.UserId);

        // 1. PostId→VenueId mapping'i kaydet (PostLiked için gerekli)
        await _mappingRepository.AddAsync(new PostVenueMapping
        {
            PostId = dto.PostId,
            VenueId = dto.VenueId,
            UserId = dto.UserId,
            CreatedAt = dto.CreatedDate != default ? dto.CreatedDate : DateTime.UtcNow
        }, context.CancellationToken);

        // 2. "Post" etkileşimini kaydet
        double baseWeight = _weightProvider.GetWeight("Post");
        var sessionId = _sessionService.GetActiveSession(dto.UserId) ?? _sessionService.StartSession(dto.UserId);
        var occurredAt = dto.CreatedDate != default ? dto.CreatedDate : DateTime.UtcNow;
        var timeDecayWeight = _timeDecayService.ComputeWeight(baseWeight, occurredAt, sessionId, DateTime.UtcNow, sessionId);

        var log = new InteractionLog
        {
            UserId = dto.UserId,
            VenueId = dto.VenueId,
            SessionId = sessionId,
            InteractionType = "Post",
            Weight = baseWeight,
            TimeDecayWeight = timeDecayWeight,
            Timestamp = occurredAt
        };

        await _interactionRepository.LogInteractionAsync(log, context.CancellationToken);
        _cacheInvalidator.InvalidateForUser(dto.UserId);
        await _idempotency.RecordAsync(eventId, "SocialServicePostCreatedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Logged Post interaction and mapping: User='{UserId}', Venue='{VenueId}', PostId='{PostId}', Weight={Weight}",
            dto.UserId, dto.VenueId, dto.PostId, baseWeight);
    }
}
