using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using CityDiscovery.RecommendationService.Domain.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for VenueService's VenueUpdatedEvent.
///
/// VenueUpdatedEvent Name + Description içerir; Categories içermez.
/// Bu consumer event alındıktan sonra VenueService REST API'sine HTTP çağrısı yaparak
/// güncel kategori listesini çeker ve embedding'i yeniden hesaplar.
/// </summary>
public class VenueUpdatedAdapterConsumer : IConsumer<VenueServiceVenueUpdatedDto>
{
    private readonly IVenueFeatureExtractor _featureExtractor;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVenueEmbeddingRepository _repository;
    private readonly IVenueServiceClient _venueServiceClient;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<VenueUpdatedAdapterConsumer> _logger;

    public VenueUpdatedAdapterConsumer(
        IVenueFeatureExtractor featureExtractor,
        IEmbeddingService embeddingService,
        IVenueEmbeddingRepository repository,
        IVenueServiceClient venueServiceClient,
        IIdempotencyService idempotency,
        ILogger<VenueUpdatedAdapterConsumer> logger)
    {
        _featureExtractor = featureExtractor;
        _embeddingService = embeddingService;
        _repository = repository;
        _venueServiceClient = venueServiceClient;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VenueServiceVenueUpdatedDto> context)
    {
        var dto = context.Message.VenueId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<VenueServiceVenueUpdatedDto>(context);

        if (dto is null || dto.VenueId == Guid.Empty)
        {
            _logger.LogError("[Adapter] VenueUpdated could not be deserialized — VenueId is empty.");
            return;
        }

        var eventId = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid();

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] VenueUpdated '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing VenueUpdated: VenueId='{VenueId}'", dto.VenueId);

        // Fetch latest categories from VenueService REST API
        var categories = await _venueServiceClient.GetVenueCategoriesAsync(
            dto.VenueId, context.CancellationToken);

        if (categories.Count > 0)
        {
            _logger.LogInformation(
                "[Adapter] Fetched {Count} categories from VenueService for Venue '{VenueId}': [{Categories}]",
                categories.Count, dto.VenueId, string.Join(", ", categories));
        }

        var descriptionText = string.IsNullOrWhiteSpace(dto.Description) ? dto.Name : dto.Description;
        var profile = new VenueProfile(
            Description: descriptionText,
            Categories: categories
        );
        var normalizedText = _featureExtractor.ExtractFeatureText(profile);
        var newEmbeddingVector = await _embeddingService.GetVenueEmbeddingAsync(normalizedText);

        var existingEmbedding = await _repository.GetByVenueIdAsync(dto.VenueId, context.CancellationToken);

        if (existingEmbedding != null)
        {
            existingEmbedding.Embedding = newEmbeddingVector;
            existingEmbedding.Categories = categories;
            existingEmbedding.UpdatedAt = DateTime.UtcNow;
            // Update CityId only if event carries a value (address set/updated)
            if (dto.CityId.HasValue)
                existingEmbedding.CityId = dto.CityId.Value;

            await _repository.UpdateAsync(existingEmbedding, context.CancellationToken);

            _logger.LogInformation(
                "[Adapter] Updated embedding for Venue '{VenueId}'. Categories={CategoryCount}, CityId={CityId}.",
                dto.VenueId, categories.Count, existingEmbedding.CityId);
        }
        else
        {
            var venueEmbedding = new VenueEmbedding
            {
                VenueId = dto.VenueId,
                CityId = dto.CityId,
                Embedding = newEmbeddingVector,
                Categories = categories
            };

            await _repository.AddAsync(venueEmbedding, context.CancellationToken);
            _logger.LogInformation(
                "[Adapter] Created new embedding for Venue '{VenueId}' via VenueUpdated. Categories={CategoryCount}.",
                dto.VenueId, categories.Count);
        }

        await _idempotency.RecordAsync(eventId, "VenueServiceVenueUpdatedEvent", context.CancellationToken);
    }
}
