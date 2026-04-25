using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for ReviewService's ReviewCreatedEvent.
/// Maps to the same logic as ReviewSubmittedEventHandler.
/// Uses ReviewId as EventId for idempotency.
/// </summary>
public class ReviewCreatedAdapterConsumer : IConsumer<ReviewServiceReviewCreatedDto>
{
    private readonly IInteractionWeightProvider _weightProvider;
    private readonly IInteractionRepository _repository;
    private readonly ITimeDecayService _timeDecayService;
    private readonly ISessionService _sessionService;
    private readonly IRecommendationCacheInvalidator _cacheInvalidator;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<ReviewCreatedAdapterConsumer> _logger;

    public ReviewCreatedAdapterConsumer(
        IInteractionWeightProvider weightProvider,
        IInteractionRepository repository,
        ITimeDecayService timeDecayService,
        ISessionService sessionService,
        IRecommendationCacheInvalidator cacheInvalidator,
        IIdempotencyService idempotency,
        ILogger<ReviewCreatedAdapterConsumer> logger)
    {
        _weightProvider = weightProvider;
        _repository = repository;
        _timeDecayService = timeDecayService;
        _sessionService = sessionService;
        _cacheInvalidator = cacheInvalidator;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReviewServiceReviewCreatedDto> context)
    {
        var dto = context.Message.ReviewId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<ReviewServiceReviewCreatedDto>(context);

        if (dto is null || dto.ReviewId == Guid.Empty)
        {
            _logger.LogError("[Adapter] ReviewCreated could not be deserialized — ReviewId is empty.");
            return;
        }

        var eventId = dto.ReviewId;

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] ReviewCreated '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing ReviewCreated from ReviewService: ReviewId='{ReviewId}', VenueId='{VenueId}', Rating={Rating}",
            dto.ReviewId, dto.VenueId, dto.Rating);

        double baseWeight = _weightProvider.GetWeight("Review", dto.Rating);
        var sessionId = _sessionService.GetActiveSession(dto.UserId) ?? _sessionService.StartSession(dto.UserId);
        var occurredAt = dto.CreatedAt != default ? dto.CreatedAt : DateTime.UtcNow;
        var timeDecayWeight = _timeDecayService.ComputeWeight(baseWeight, occurredAt, sessionId, DateTime.UtcNow, sessionId);

        var log = new InteractionLog
        {
            UserId = dto.UserId,
            VenueId = dto.VenueId,
            SessionId = sessionId,
            InteractionType = "Review",
            Weight = baseWeight,
            TimeDecayWeight = timeDecayWeight,
            Timestamp = occurredAt
        };

        await _repository.LogInteractionAsync(log, context.CancellationToken);
        _cacheInvalidator.InvalidateForUser(dto.UserId);
        await _idempotency.RecordAsync(eventId, "ReviewServiceReviewCreatedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Logged Review interaction: User='{UserId}', Venue='{VenueId}', Weight={Weight}",
            dto.UserId, dto.VenueId, baseWeight);
    }
}
