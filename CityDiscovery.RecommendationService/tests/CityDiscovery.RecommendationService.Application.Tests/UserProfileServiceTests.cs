using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Application.Services;
using CityDiscovery.RecommendationService.Domain.Constants;
using CityDiscovery.RecommendationService.Domain.Entities;
using CityDiscovery.RecommendationService.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class UserProfileServiceTests
{
    private readonly Mock<IInteractionRepository> _interactionRepo = new();
    private readonly Mock<IVenueEmbeddingRepository> _venueEmbeddingRepo = new();
    private readonly Mock<IEmbeddingService> _embeddingService = new();
    private readonly Mock<ITimeDecayService> _timeDecayService = new();
    private readonly Mock<ISessionService> _sessionService = new();
    private readonly Mock<IUserProfileRepository> _userProfileRepo = new();
    private readonly Mock<ILogger<UserProfileService>> _logger = new();

    private UserProfileService CreateService()
    {
        return new UserProfileService(
            _interactionRepo.Object,
            _venueEmbeddingRepo.Object,
            _embeddingService.Object,
            _timeDecayService.Object,
            _sessionService.Object,
            _userProfileRepo.Object,
            _logger.Object);
    }

    [Fact]
    public async Task ComputeAndSaveUserProfileAsync_NoInteractions_SavesZeroVector()
    {
        var userId = Guid.NewGuid();
        _interactionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InteractionLog>());
        _sessionService.Setup(s => s.GetActiveSession(userId)).Returns((Guid?)null);

        var sut = CreateService();
        var result = await sut.ComputeAndSaveUserProfileAsync(userId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.Embedding.Should().HaveCount(EmbeddingConstants.Dimensions);
        result.Embedding.Should().AllSatisfy(v => v.Should().Be(0f));

        _userProfileRepo.Verify(r => r.UpsertAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ComputeAndSaveUserProfileAsync_WithInteractions_ComputesProfile()
    {
        var userId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var embedding = new float[EmbeddingConstants.Dimensions];
        embedding[0] = 0.5f;
        embedding[1] = 0.3f;

        var interactions = new List<InteractionLog>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VenueId = venueId,
                SessionId = sessionId,
                InteractionType = "like",
                Weight = 1.0,
                TimeDecayWeight = 1.0,
                Timestamp = DateTime.UtcNow
            }
        };

        _interactionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactions);
        _sessionService.Setup(s => s.GetActiveSession(userId)).Returns(sessionId);
        _venueEmbeddingRepo.Setup(r => r.GetByVenueIdAsync(venueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VenueEmbedding
            {
                VenueId = venueId,
                Embedding = embedding,
                CityId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        _timeDecayService.Setup(t => t.ComputeWeight(
                It.IsAny<double>(), It.IsAny<DateTime>(), It.IsAny<Guid>(),
                It.IsAny<DateTime?>(), It.IsAny<Guid?>()))
            .Returns(0.95);
        _embeddingService.Setup(e => e.ComputeUserEmbedding(It.IsAny<IEnumerable<InteractionVector>>()))
            .Returns(embedding);

        var sut = CreateService();
        var result = await sut.ComputeAndSaveUserProfileAsync(userId);

        result.Embedding[0].Should().Be(0.5f);
        _embeddingService.Verify(e => e.ComputeUserEmbedding(It.IsAny<IEnumerable<InteractionVector>>()), Times.Once);
    }

    [Fact]
    public async Task ComputeAndSaveUserProfileAsync_VenueNotFound_SkipsInteraction()
    {
        var userId = Guid.NewGuid();
        var venueId = Guid.NewGuid();

        var interactions = new List<InteractionLog>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VenueId = venueId,
                SessionId = Guid.NewGuid(),
                InteractionType = "like",
                Weight = 1.0,
                TimeDecayWeight = 1.0,
                Timestamp = DateTime.UtcNow
            }
        };

        _interactionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactions);
        _sessionService.Setup(s => s.GetActiveSession(userId)).Returns((Guid?)null);
        _venueEmbeddingRepo.Setup(r => r.GetByVenueIdAsync(venueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VenueEmbedding?)null); // venue bulunamadı

        var sut = CreateService();
        var result = await sut.ComputeAndSaveUserProfileAsync(userId);

        // Venue olmadığı için zero vector
        result.Embedding.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GetUserEmbeddingAsync_ZeroVector_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        _interactionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InteractionLog>());
        _sessionService.Setup(s => s.GetActiveSession(userId)).Returns((Guid?)null);

        var sut = CreateService();
        var result = await sut.GetUserEmbeddingAsync(userId);

        result.Should().BeNull();
    }
}
