using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Constants;
using CityDiscovery.RecommendationService.Domain.Entities;
using CityDiscovery.RecommendationService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Services;

public class UserProfileService : IUserProfileService
{
    private readonly IInteractionRepository _interactionRepo;
    private readonly IVenueEmbeddingRepository _venueEmbeddingRepo;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITimeDecayService _timeDecayService;
    private readonly ISessionService _sessionService;
    private readonly IUserProfileRepository _userProfileRepo;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        IInteractionRepository interactionRepo,
        IVenueEmbeddingRepository venueEmbeddingRepo,
        IEmbeddingService embeddingService,
        ITimeDecayService timeDecayService,
        ISessionService sessionService,
        IUserProfileRepository userProfileRepo,
        ILogger<UserProfileService> logger)
    {
        _interactionRepo = interactionRepo;
        _venueEmbeddingRepo = venueEmbeddingRepo;
        _embeddingService = embeddingService;
        _timeDecayService = timeDecayService;
        _sessionService = sessionService;
        _userProfileRepo = userProfileRepo;
        _logger = logger;
    }

    public async Task<UserProfile> ComputeAndSaveUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var interactions = await _interactionRepo.GetByUserIdAsync(userId, cancellationToken);
        var activeSessionId = _sessionService.GetActiveSession(userId);
        var now = DateTime.UtcNow;

        var vectors = new List<InteractionVector>();

        foreach (var log in interactions)
        {
            var venueEmbedding = await _venueEmbeddingRepo.GetByVenueIdAsync(log.VenueId, cancellationToken);
            if (venueEmbedding == null || venueEmbedding.Embedding.Length == 0)
                continue;

            var timeDecayWeight = _timeDecayService.ComputeWeight(
                log.Weight, log.Timestamp, log.SessionId, now, activeSessionId);

            vectors.Add(new InteractionVector(venueEmbedding.Embedding, (float)timeDecayWeight));
        }

        float[] embedding;
        if (vectors.Count == 0)
        {
            embedding = new float[EmbeddingConstants.Dimensions];
        }
        else
        {
            embedding = _embeddingService.ComputeUserEmbedding(vectors);
        }

        var profile = new UserProfile
        {
            UserId = userId,
            Embedding = embedding,
            LastUpdatedAt = now,
            ActiveSessionId = activeSessionId
        };

        await _userProfileRepo.UpsertAsync(profile, cancellationToken);
        _logger.LogInformation("Computed and saved user profile for User {UserId}", userId);
        return profile;
    }

    public async Task<float[]?> GetUserEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await ComputeAndSaveUserProfileAsync(userId, cancellationToken);
        if (profile.Embedding.All(x => x == 0))
            return null;
        return profile.Embedding;
    }
}
