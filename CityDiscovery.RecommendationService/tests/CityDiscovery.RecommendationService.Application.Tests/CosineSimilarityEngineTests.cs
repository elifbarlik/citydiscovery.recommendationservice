using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Application.Services;
using CityDiscovery.RecommendationService.Domain.Constants;
using CityDiscovery.RecommendationService.Domain.Entities;
using CityDiscovery.RecommendationService.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class CosineSimilarityEngineTests
{
    private readonly Mock<IUserProfileService> _userProfileService = new();
    private readonly Mock<IVenueEmbeddingRepository> _venueEmbeddingRepo = new();
    private readonly Mock<IInteractionRepository> _interactionRepo = new();
    private readonly Mock<IDismissedVenueRepository> _dismissedVenueRepo = new();
    private readonly Mock<IUserPreferenceRepository> _userPreferenceRepo = new();
    private readonly Mock<IHybridScoringService> _hybridScoring = new();
    private readonly Mock<ILogger<CosineSimilarityRecommendationEngine>> _logger = new();

    private CosineSimilarityRecommendationEngine CreateEngine()
    {
        return new CosineSimilarityRecommendationEngine(
            _userProfileService.Object,
            _venueEmbeddingRepo.Object,
            _interactionRepo.Object,
            _dismissedVenueRepo.Object,
            _userPreferenceRepo.Object,
            _hybridScoring.Object,
            _logger.Object);
    }

    private float[] CreateEmbedding(float value = 0.5f)
    {
        var emb = new float[EmbeddingConstants.Dimensions];
        emb[0] = value;
        return emb;
    }

    [Fact]
    public async Task GetRecommendationsAsync_NoUserProfile_UsesFallback()
    {
        var userId = Guid.NewGuid();
        var cityId = 1;

        _userProfileService.Setup(s => s.GetUserEmbeddingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);
        _interactionRepo.Setup(r => r.GetMostInteractedVenueIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _venueEmbeddingRepo.Setup(r => r.GetAllByCityIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>());
        _venueEmbeddingRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>());
        _interactionRepo.Setup(r => r.GetInteractedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _dismissedVenueRepo.Setup(r => r.GetDismissedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _userPreferenceRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreference?)null);

        var sut = CreateEngine();
        var result = await sut.GetRecommendationsAsync(userId, cityId, 10);

        result.Strategy.Should().Be("popularity_fallback");
    }

    [Fact]
    public async Task GetRecommendationsAsync_WithProfile_UsesHybridStrategy()
    {
        var userId = Guid.NewGuid();
        var cityId = 1;
        var venueId = Guid.NewGuid();

        _userProfileService.Setup(s => s.GetUserEmbeddingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmbedding());

        _venueEmbeddingRepo.Setup(r => r.GetAllByCityIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>
            {
                new()
                {
                    VenueId = venueId,
                    Embedding = CreateEmbedding(0.8f),
                    CityId = cityId,
                    Categories = new List<string> { "restaurant" },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            });

        _interactionRepo.Setup(r => r.GetInteractedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _dismissedVenueRepo.Setup(r => r.GetDismissedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var scoredVenues = new List<ScoredVenue> { new(venueId, 0.9, "hybrid", false) };
        _hybridScoring.Setup(h => h.ComputeHybridScoresAsync(
                It.IsAny<IReadOnlyList<VenueScoreInput>>(), userId, It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scoredVenues);

        var sut = CreateEngine();
        var result = await sut.GetRecommendationsAsync(userId, cityId, 10);

        result.Strategy.Should().Be("hybrid");
        result.Venues.Should().HaveCount(1);
        result.Venues[0].VenueId.Should().Be(venueId);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ExcludesInteractedVenues()
    {
        var userId = Guid.NewGuid();
        var cityId = 1;
        var interactedVenueId = Guid.NewGuid();
        var freshVenueId = Guid.NewGuid();

        _userProfileService.Setup(s => s.GetUserEmbeddingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmbedding());

        _venueEmbeddingRepo.Setup(r => r.GetAllByCityIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>
            {
                new() { VenueId = interactedVenueId, Embedding = CreateEmbedding(), CityId = cityId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { VenueId = freshVenueId, Embedding = CreateEmbedding(), CityId = cityId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });

        _interactionRepo.Setup(r => r.GetInteractedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { interactedVenueId });
        _dismissedVenueRepo.Setup(r => r.GetDismissedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        _hybridScoring.Setup(h => h.ComputeHybridScoresAsync(
                It.Is<IReadOnlyList<VenueScoreInput>>(c => c.All(v => v.VenueId != interactedVenueId)),
                userId, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredVenue> { new(freshVenueId, 0.8, "hybrid", false) });

        var sut = CreateEngine();
        var result = await sut.GetRecommendationsAsync(userId, cityId, 10);

        result.Venues.Should().NotContain(v => v.VenueId == interactedVenueId);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ExcludesDismissedVenues()
    {
        var userId = Guid.NewGuid();
        var cityId = 1;
        var dismissedVenueId = Guid.NewGuid();
        var freshVenueId = Guid.NewGuid();

        _userProfileService.Setup(s => s.GetUserEmbeddingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmbedding());

        _venueEmbeddingRepo.Setup(r => r.GetAllByCityIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>
            {
                new() { VenueId = dismissedVenueId, Embedding = CreateEmbedding(), CityId = cityId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { VenueId = freshVenueId, Embedding = CreateEmbedding(), CityId = cityId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });

        _interactionRepo.Setup(r => r.GetInteractedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _dismissedVenueRepo.Setup(r => r.GetDismissedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { dismissedVenueId });

        _hybridScoring.Setup(h => h.ComputeHybridScoresAsync(
                It.Is<IReadOnlyList<VenueScoreInput>>(c => c.All(v => v.VenueId != dismissedVenueId)),
                userId, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredVenue> { new(freshVenueId, 0.8, "hybrid", false) });

        var sut = CreateEngine();
        var result = await sut.GetRecommendationsAsync(userId, cityId, 10);

        result.Venues.Should().NotContain(v => v.VenueId == dismissedVenueId);
    }

    [Fact]
    public async Task GetRecommendationsAsync_CategoryFilter_FiltersCorrectly()
    {
        var userId = Guid.NewGuid();
        var cityId = 1;
        var cafeVenueId = Guid.NewGuid();
        var restaurantVenueId = Guid.NewGuid();

        _userProfileService.Setup(s => s.GetUserEmbeddingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmbedding());

        _venueEmbeddingRepo.Setup(r => r.GetAllByCityIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>
            {
                new() { VenueId = cafeVenueId, Embedding = CreateEmbedding(), CityId = cityId, Categories = new() { "cafe" }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { VenueId = restaurantVenueId, Embedding = CreateEmbedding(), CityId = cityId, Categories = new() { "restaurant" }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });

        _interactionRepo.Setup(r => r.GetInteractedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _dismissedVenueRepo.Setup(r => r.GetDismissedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        // Only cafe should pass filter
        _hybridScoring.Setup(h => h.ComputeHybridScoresAsync(
                It.Is<IReadOnlyList<VenueScoreInput>>(c => c.Count == 1 && c[0].VenueId == cafeVenueId),
                userId, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredVenue> { new(cafeVenueId, 0.8, "hybrid", false) });

        var sut = CreateEngine();
        var result = await sut.GetRecommendationsAsync(userId, cityId, 10, categories: new List<string> { "cafe" });

        result.Venues.Should().HaveCount(1);
        result.Venues[0].VenueId.Should().Be(cafeVenueId);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ColdStartWithPreferences_UsesPreferenceFallback()
    {
        var userId = Guid.NewGuid();
        var cityId = 1;
        var venueId = Guid.NewGuid();

        _userProfileService.Setup(s => s.GetUserEmbeddingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);

        _interactionRepo.Setup(r => r.GetMostInteractedVenueIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { venueId });
        _venueEmbeddingRepo.Setup(r => r.GetAllByCityIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>());
        _venueEmbeddingRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VenueEmbedding>
            {
                new() { VenueId = venueId, Embedding = CreateEmbedding(), CityId = cityId, Categories = new() { "museum" }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });
        _interactionRepo.Setup(r => r.GetInteractedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _dismissedVenueRepo.Setup(r => r.GetDismissedVenueIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _userPreferenceRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreference
            {
                UserId = userId,
                PreferredCategories = new List<string> { "museum" },
                UpdatedAt = DateTime.UtcNow
            });

        var sut = CreateEngine();
        var result = await sut.GetRecommendationsAsync(userId, cityId, 10);

        result.Strategy.Should().Be("preference_fallback");
    }
}
