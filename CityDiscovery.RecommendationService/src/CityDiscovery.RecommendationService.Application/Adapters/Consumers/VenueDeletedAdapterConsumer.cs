using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for VenueService's VenueDeletedEvent.
///
/// Mekan silindiğinde RecommendationService'deki tüm ilgili verileri temizler:
/// - VenueEmbedding: vektör kaydı
/// - InteractionLog: bu mekana ait tüm etkileşimler
/// - DismissedVenue: bu mekana ait dismiss kayıtları
/// - PostVenueMapping: bu mekana ait post mapping'leri
/// </summary>
public class VenueDeletedAdapterConsumer : IConsumer<VenueServiceVenueDeletedDto>
{
    private readonly IVenueEmbeddingRepository _embeddingRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IDismissedVenueRepository _dismissedVenueRepository;
    private readonly IPostVenueMappingRepository _postVenueMappingRepository;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<VenueDeletedAdapterConsumer> _logger;

    public VenueDeletedAdapterConsumer(
        IVenueEmbeddingRepository embeddingRepository,
        IInteractionRepository interactionRepository,
        IDismissedVenueRepository dismissedVenueRepository,
        IPostVenueMappingRepository postVenueMappingRepository,
        IIdempotencyService idempotency,
        ILogger<VenueDeletedAdapterConsumer> logger)
    {
        _embeddingRepository = embeddingRepository;
        _interactionRepository = interactionRepository;
        _dismissedVenueRepository = dismissedVenueRepository;
        _postVenueMappingRepository = postVenueMappingRepository;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VenueServiceVenueDeletedDto> context)
    {
        var dto = context.Message.VenueId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<VenueServiceVenueDeletedDto>(context);

        if (dto is null || dto.VenueId == Guid.Empty)
        {
            _logger.LogError("[Adapter] VenueDeleted could not be deserialized — VenueId is empty.");
            return;
        }

        var eventId = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid();

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] VenueDeleted '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing VenueDeleted: VenueId='{VenueId}', VenueName='{VenueName}'",
            dto.VenueId, dto.VenueName);

        await _embeddingRepository.DeleteByVenueIdAsync(dto.VenueId, context.CancellationToken);
        await _interactionRepository.DeleteByVenueIdAsync(dto.VenueId, context.CancellationToken);
        await _dismissedVenueRepository.DeleteByVenueIdAsync(dto.VenueId, context.CancellationToken);
        await _postVenueMappingRepository.DeleteByVenueIdAsync(dto.VenueId, context.CancellationToken);

        await _idempotency.RecordAsync(eventId, "VenueServiceVenueDeletedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Cleaned up all data for deleted venue '{VenueId}'.", dto.VenueId);
    }
}
