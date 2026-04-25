using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for SocialService's PostSavedEvent.
///
/// PostSavedEvent'te VenueId bulunmaz. VenueId'yi PostVenueMappings tablosundan
/// PostId üzerinden lookup ederiz. PostCreatedAdapterConsumer bu tabloyu doldurur.
/// Mapping bulunamazsa event loglanır ve atlanır.
/// </summary>
public class PostSavedAdapterConsumer : IConsumer<SocialServicePostSavedDto>
{
    private readonly IPostVenueMappingRepository _mappingRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IInteractionWeightProvider _weightProvider;
    private readonly ITimeDecayService _timeDecayService;
    private readonly ISessionService _sessionService;
    private readonly IRecommendationCacheInvalidator _cacheInvalidator;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<PostSavedAdapterConsumer> _logger;

    public PostSavedAdapterConsumer(
        IPostVenueMappingRepository mappingRepository,
        IInteractionRepository interactionRepository,
        IInteractionWeightProvider weightProvider,
        ITimeDecayService timeDecayService,
        ISessionService sessionService,
        IRecommendationCacheInvalidator cacheInvalidator,
        IIdempotencyService idempotency,
        ILogger<PostSavedAdapterConsumer> logger)
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

    public async Task Consume(ConsumeContext<SocialServicePostSavedDto> context)
    {
        var dto = context.Message.PostId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<SocialServicePostSavedDto>(context);

        if (dto is null || dto.PostId == Guid.Empty)
        {
            _logger.LogError("[Adapter] PostSaved could not be deserialized — PostId is empty.");
            return;
        }

        var eventId = GenerateDeterministicId(dto.UserId, dto.PostId, dto.SavedAt);

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] PostSaved '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        var venueId = await _mappingRepository.GetVenueIdByPostIdAsync(dto.PostId, context.CancellationToken);

        if (venueId is null)
        {
            _logger.LogWarning(
                "[Adapter] PostSaved received but PostId='{PostId}' has no VenueId mapping. " +
                "PostCreatedEvent may not have been received yet. UserId='{UserId}'. Skipping.",
                dto.PostId, dto.UserId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing PostSaved: PostId='{PostId}', VenueId='{VenueId}', UserId='{UserId}'",
            dto.PostId, venueId, dto.UserId);

        double baseWeight = _weightProvider.GetWeight("Save");
        var sessionId = _sessionService.GetActiveSession(dto.UserId) ?? _sessionService.StartSession(dto.UserId);
        var occurredAt = dto.SavedAt != default ? dto.SavedAt : DateTime.UtcNow;
        var timeDecayWeight = _timeDecayService.ComputeWeight(baseWeight, occurredAt, sessionId, DateTime.UtcNow, sessionId);

        var log = new InteractionLog
        {
            UserId = dto.UserId,
            VenueId = venueId.Value,
            SessionId = sessionId,
            InteractionType = "Save",
            Weight = baseWeight,
            TimeDecayWeight = timeDecayWeight,
            Timestamp = occurredAt
        };

        await _interactionRepository.LogInteractionAsync(log, context.CancellationToken);
        _cacheInvalidator.InvalidateForUser(dto.UserId);
        await _idempotency.RecordAsync(eventId, "SocialServicePostSavedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Logged Save interaction: User='{UserId}', Venue='{VenueId}', Weight={Weight}",
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
