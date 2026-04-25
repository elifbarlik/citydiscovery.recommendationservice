using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;

using CityDiscovery.RecommendationService.Domain.Entities;
using CityDiscovery.RecommendationService.Domain.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for VenueService's VenueCreatedEvent.
///
/// VenueCreatedEvent yalnızca Name içerir; Description, Categories ve CityId içermez.
/// Bu consumer event alındıktan sonra VenueService REST API'sine HTTP çağrısı yaparak
/// kategori bilgilerini çeker ve embedding'i zenginleştirir.
///
/// NOT: CityId VenueService API'sinden de çekilememektedir (endpoint mevcut değil).
/// CityId null bırakılır; öneri motoru fallback olarak GetAllAsync kullanır.
/// </summary>
public class VenueCreatedAdapterConsumer : IConsumer<VenueServiceVenueCreatedDto>
{
    private readonly IVenueFeatureExtractor _featureExtractor;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVenueEmbeddingRepository _repository;
    private readonly IVenueServiceClient _venueServiceClient;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<VenueCreatedAdapterConsumer> _logger;

    public VenueCreatedAdapterConsumer(
        IVenueFeatureExtractor featureExtractor,
        IEmbeddingService embeddingService,
        IVenueEmbeddingRepository repository,
        IVenueServiceClient venueServiceClient,
        IIdempotencyService idempotency,
        ILogger<VenueCreatedAdapterConsumer> logger)
    {
        _featureExtractor = featureExtractor;
        _embeddingService = embeddingService;
        _repository = repository;
        _venueServiceClient = venueServiceClient;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VenueServiceVenueCreatedDto> context)
    {
        // MassTransit raw deserializer does not unwrap the "message" field from the
        // application/vnd.masstransit+json envelope — it maps the outer envelope to
        // the DTO, resulting in Guid.Empty for all Guid fields. Fall back to manual
        // envelope unwrapping when VenueId is empty.
        var dto = context.Message.VenueId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<VenueServiceVenueCreatedDto>(context);

        if (dto is null || dto.VenueId == Guid.Empty)
        {
            _logger.LogError(
                "[Adapter] VenueCreated could not be deserialized — VenueId is empty after envelope unwrap. " +
                "ContentType='{ContentType}'", context.ReceiveContext.ContentType);
            return;
        }

        var eventId = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid();

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] VenueCreated '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing VenueCreated: VenueId='{VenueId}', Name='{Name}'",
            dto.VenueId, dto.Name);

        // Fetch categories from VenueService REST API
        var categories = await _venueServiceClient.GetVenueCategoriesAsync(
            dto.VenueId, context.CancellationToken);

        if (categories.Count > 0)
        {
            _logger.LogInformation(
                "[Adapter] Enriched VenueCreated with {Count} categories from VenueService: [{Categories}]",
                categories.Count, string.Join(", ", categories));
        }
        else
        {
            _logger.LogWarning(
                "[Adapter] No categories returned from VenueService for Venue '{VenueId}'. " +
                "Venue may not have categories yet or VenueService is unreachable.",
                dto.VenueId);
        }

        // Build embedding: Name as description + fetched categories
        var profile = new VenueProfile(
            Description: dto.Name,
            Categories: categories
        );
        var normalizedText = _featureExtractor.ExtractFeatureText(profile);
        var embeddingVector = await _embeddingService.GetVenueEmbeddingAsync(normalizedText);

        var venueEmbedding = new VenueEmbedding
        {
            VenueId = dto.VenueId,
            CityId = null, // VenueService API'sinde CityId endpoint'i mevcut değil
            Embedding = embeddingVector,
            Categories = categories
        };

        await _repository.AddAsync(venueEmbedding, context.CancellationToken);
        await _idempotency.RecordAsync(eventId, "VenueServiceVenueCreatedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] VenueCreated processed. VenueId='{VenueId}', Categories={CategoryCount}. " +
            "Note: CityId is null — city-based filtering will use global fallback until VenueService exposes CityId.",
            dto.VenueId, categories.Count);
    }
}
