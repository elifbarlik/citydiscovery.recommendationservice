using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Application.Options;
using CityDiscovery.RecommendationService.Application.Services;
using CityDiscovery.RecommendationService.Domain.Entities;
using CityDiscovery.RecommendationService.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class HybridScoringServiceTests
{
    private readonly Mock<IInteractionRepository> _interactionRepo = new();
    private readonly Mock<IVenueEmbeddingRepository> _venueEmbeddingRepo = new();
    private readonly RecommendationWeightsOptions _weights = new()
    {
        EmbeddingSimilarity = 0.5,
        Popularity = 0.2,
        Recency = 0.15,
        SessionAffinity = 0.1,
        DiversityPenalty = 0.05
    };

    private HybridScoringService CreateService()
    {
        return new HybridScoringService(
            _interactionRepo.Object,
            _venueEmbeddingRepo.Object,
            Microsoft.Extensions.Options.Options.Create(_weights));
    }

    [Fact]
    public async Task ComputeHybridScoresAsync_EmptyCandidates_ReturnsEmpty()
    {
        var sut = CreateService();
        var result = await sut.ComputeHybridScoresAsync(
            Array.Empty<VenueScoreInput>(), Guid.NewGuid(), null, 10, 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeHybridScoresAsync_SingleCandidate_ReturnsScored()
    {
        var venueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var candidates = new List<VenueScoreInput>
        {
            new(venueId, 0.9, new List<string> { "restaurant" })
        };

        _interactionRepo.Setup(r => r.GetInteractionCountsByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { venueId, 5 } });

        _interactionRepo.Setup(r => r.GetLastInteractionDatesByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime> { { venueId, DateTime.UtcNow.AddDays(-3) } });

        _interactionRepo.Setup(r => r.GetVenueIdsBySessionAsync(
                userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var sut = CreateService();
        var result = await sut.ComputeHybridScoresAsync(candidates, userId, null, 10, 0);

        result.Should().HaveCount(1);
        result[0].VenueId.Should().Be(venueId);
        result[0].Score.Should().BeGreaterThan(0);
        result[0].Strategy.Should().Be("hybrid");
    }

    [Fact]
    public async Task ComputeHybridScoresAsync_RespectsLimit()
    {
        var candidates = Enumerable.Range(0, 20).Select(i =>
            new VenueScoreInput(Guid.NewGuid(), 0.5 + i * 0.01, new List<string> { "cafe" })
        ).ToList();

        _interactionRepo.Setup(r => r.GetInteractionCountsByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        _interactionRepo.Setup(r => r.GetLastInteractionDatesByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        _interactionRepo.Setup(r => r.GetVenueIdsBySessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var sut = CreateService();
        var result = await sut.ComputeHybridScoresAsync(candidates, Guid.NewGuid(), null, 5, 0);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task ComputeHybridScoresAsync_RespectsOffset()
    {
        var candidates = Enumerable.Range(0, 10).Select(i =>
            new VenueScoreInput(Guid.NewGuid(), 1.0 - i * 0.1, new List<string> { "cafe" })
        ).ToList();

        _interactionRepo.Setup(r => r.GetInteractionCountsByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _interactionRepo.Setup(r => r.GetLastInteractionDatesByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());
        _interactionRepo.Setup(r => r.GetVenueIdsBySessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var sut = CreateService();
        var result = await sut.ComputeHybridScoresAsync(candidates, Guid.NewGuid(), null, 5, 3);

        result.Should().HaveCount(5);
        // Offset 3 means skip top 3
        result[0].VenueId.Should().NotBe(candidates[0].VenueId);
    }

    [Fact]
    public async Task ComputeHybridScoresAsync_HigherSimilarity_HigherScore()
    {
        var venueHigh = Guid.NewGuid();
        var venueLow = Guid.NewGuid();
        var candidates = new List<VenueScoreInput>
        {
            new(venueHigh, 0.95, new List<string> { "cafe" }),
            new(venueLow, 0.1, new List<string> { "cafe" })
        };

        _interactionRepo.Setup(r => r.GetInteractionCountsByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _interactionRepo.Setup(r => r.GetLastInteractionDatesByVenueIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());
        _interactionRepo.Setup(r => r.GetVenueIdsBySessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var sut = CreateService();
        var result = await sut.ComputeHybridScoresAsync(candidates, Guid.NewGuid(), null, 10, 0);

        result[0].VenueId.Should().Be(venueHigh);
        result[0].Score.Should().BeGreaterThan(result[1].Score);
    }
}
