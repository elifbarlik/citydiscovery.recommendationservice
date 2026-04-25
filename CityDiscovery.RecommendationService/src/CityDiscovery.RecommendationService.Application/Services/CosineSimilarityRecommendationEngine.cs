using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Constants;
using CityDiscovery.RecommendationService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Services;

public class CosineSimilarityRecommendationEngine : IRecommendationEngine
{
    private readonly IUserProfileService _userProfileService;
    private readonly IVenueEmbeddingRepository _venueEmbeddingRepo;
    private readonly IInteractionRepository _interactionRepo;
    private readonly IDismissedVenueRepository _dismissedVenueRepo;
    private readonly IUserPreferenceRepository _userPreferenceRepo;
    private readonly IHybridScoringService _hybridScoring;
    private readonly ILogger<CosineSimilarityRecommendationEngine> _logger;

    public CosineSimilarityRecommendationEngine(
        IUserProfileService userProfileService,
        IVenueEmbeddingRepository venueEmbeddingRepo,
        IInteractionRepository interactionRepo,
        IDismissedVenueRepository dismissedVenueRepo,
        IUserPreferenceRepository userPreferenceRepo,
        IHybridScoringService hybridScoring,
        ILogger<CosineSimilarityRecommendationEngine> logger)
    {
        _userProfileService = userProfileService;
        _venueEmbeddingRepo = venueEmbeddingRepo;
        _interactionRepo = interactionRepo;
        _dismissedVenueRepo = dismissedVenueRepo;
        _userPreferenceRepo = userPreferenceRepo;
        _hybridScoring = hybridScoring;
        _logger = logger;
    }

    public async Task<RecommendationResult> GetRecommendationsAsync(
        Guid userId,
        int cityId,
        int limit,
        int offset = 0,
        Guid? sessionId = null,
        List<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var userEmbedding = await _userProfileService.GetUserEmbeddingAsync(userId, cancellationToken);

        if (userEmbedding == null || userEmbedding.All(x => x == 0))
        {
            _logger.LogInformation("No user profile for {UserId}, falling back to preference/popularity", userId);
            return await GetColdStartFallbackAsync(userId, cityId, limit, offset, categories, cancellationToken);
        }

        var venues = await _venueEmbeddingRepo.GetAllByCityIdAsync(cityId, cancellationToken);
        if (venues.Count == 0)
        {
            venues = (await _venueEmbeddingRepo.GetAllAsync(cancellationToken)).ToList();
        }

        // Build exclusion set: interacted + dismissed venues
        var excludedVenueIds = await _interactionRepo.GetInteractedVenueIdsAsync(userId, cancellationToken);
        var dismissedVenueIds = await _dismissedVenueRepo.GetDismissedVenueIdsAsync(userId, cancellationToken);
        var excludedSet = excludedVenueIds.Concat(dismissedVenueIds).ToHashSet();

        // Apply category filter if specified
        var categorySet = categories?.Select(c => c.ToLowerInvariant()).ToHashSet();

        var candidates = new List<VenueScoreInput>();
        foreach (var v in venues)
        {
            if (excludedSet.Contains(v.VenueId))
                continue;

            // Category filtering
            if (categorySet != null && categorySet.Count > 0)
            {
                var venueCategories = v.Categories?.Select(c => c.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
                if (!venueCategories.Overlaps(categorySet))
                    continue;
            }

            var similarity = CosineSimilarity(userEmbedding, v.Embedding);
            candidates.Add(new VenueScoreInput(v.VenueId, similarity, v.Categories ?? new List<string>()));
        }

        var scored = await _hybridScoring.ComputeHybridScoresAsync(
            candidates, userId, sessionId, limit, offset, cancellationToken);

        return new RecommendationResult(scored, "hybrid");
    }

    /// <summary>
    /// Cold-start: if user has preferences, boost those categories; otherwise pure popularity.
    /// </summary>
    private async Task<RecommendationResult> GetColdStartFallbackAsync(
        Guid userId,
        int cityId,
        int limit,
        int offset,
        List<string>? categories,
        CancellationToken cancellationToken)
    {
        var venueEmbeddings = await _venueEmbeddingRepo.GetAllByCityIdAsync(cityId, cancellationToken);
        if (venueEmbeddings.Count == 0)
            venueEmbeddings = (await _venueEmbeddingRepo.GetAllAsync(cancellationToken)).ToList();
        var popularity = await _interactionRepo.GetMostInteractedVenueIdsAsync((offset + limit) * 2, cancellationToken);
        var venueMap = venueEmbeddings.ToDictionary(v => v.VenueId);

        var excludedVenueIds = await _interactionRepo.GetInteractedVenueIdsAsync(userId, cancellationToken);
        var dismissedVenueIds = await _dismissedVenueRepo.GetDismissedVenueIdsAsync(userId, cancellationToken);
        var excludedSet = excludedVenueIds.Concat(dismissedVenueIds).ToHashSet();

        // Build category filter: explicit param > user preferences
        var categorySet = categories?.Select(c => c.ToLowerInvariant()).ToHashSet();
        if (categorySet == null || categorySet.Count == 0)
        {
            var prefs = await _userPreferenceRepo.GetByUserIdAsync(userId, cancellationToken);
            if (prefs != null && prefs.PreferredCategories.Count > 0)
            {
                categorySet = prefs.PreferredCategories.Select(c => c.ToLowerInvariant()).ToHashSet();
                _logger.LogInformation("Cold-start for {UserId}: using user preferences [{Categories}]",
                    userId, string.Join(", ", categorySet));
            }
        }

        var result = popularity
            .Where(id => !excludedSet.Contains(id) && venueMap.ContainsKey(id))
            .Where(id =>
            {
                if (categorySet == null || categorySet.Count == 0) return true;
                var venueCategories = venueMap[id].Categories?.Select(c => c.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
                return venueCategories.Overlaps(categorySet);
            })
            .Skip(offset)
            .Take(limit)
            .Select(id => new ScoredVenue(id, 1.0, "popularity_fallback", false))
            .ToList();

        var strategy = categorySet != null && categorySet.Count > 0 ? "preference_fallback" : "popularity_fallback";
        return new RecommendationResult(result, strategy);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom < 1e-10 ? 0 : dot / denom;
    }
}
