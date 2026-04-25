using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Application.Options;
using CityDiscovery.RecommendationService.Domain.Models;
using Microsoft.Extensions.Options;

namespace CityDiscovery.RecommendationService.Application.Services;

public class HybridScoringService : IHybridScoringService
{
    private readonly IInteractionRepository _interactionRepo;
    private readonly IVenueEmbeddingRepository _venueEmbeddingRepo;
    private readonly RecommendationWeightsOptions _weights;

    private static readonly TimeSpan Recency7Days = TimeSpan.FromDays(7);
    private static readonly TimeSpan Recency30Days = TimeSpan.FromDays(30);

    public HybridScoringService(
        IInteractionRepository interactionRepo,
        IVenueEmbeddingRepository venueEmbeddingRepo,
        IOptions<RecommendationWeightsOptions> weights)
    {
        _interactionRepo = interactionRepo;
        _venueEmbeddingRepo = venueEmbeddingRepo;
        _weights = weights.Value;
    }

    public async Task<IReadOnlyList<ScoredVenue>> ComputeHybridScoresAsync(
        IReadOnlyList<VenueScoreInput> candidates,
        Guid userId,
        Guid? sessionId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
            return Array.Empty<ScoredVenue>();

        var venueIds = candidates.Select(c => c.VenueId).ToList();
        var now = DateTime.UtcNow;

        // popularity_score: min-max normalization
        var counts = await _interactionRepo.GetInteractionCountsByVenueIdsAsync(venueIds, cancellationToken);
        var countValues = venueIds.Select(id => counts.GetValueOrDefault(id, 0)).ToList();
        var minCount = countValues.Min();
        var maxCount = countValues.Max();
        var rangeCount = maxCount - minCount;
        var popularityByVenue = venueIds.ToDictionary(id => id, id =>
            rangeCount > 0 ? (counts.GetValueOrDefault(id, 0) - minCount) / (double)rangeCount : 0.0);

        // recency_boost: last 7d → 0.1, last 30d → 0.05, else 0
        var lastDates = await _interactionRepo.GetLastInteractionDatesByVenueIdsAsync(venueIds, cancellationToken);
        var recencyByVenue = venueIds.ToDictionary(id => id, id =>
        {
            if (!lastDates.TryGetValue(id, out var last))
                return 0.0;
            var age = now - last;
            if (age <= Recency7Days) return 0.1;
            if (age <= Recency30Days) return 0.05;
            return 0.0;
        });

        // session_affinity_score: overlap with session-interaction venue categories
        var sessionCategories = await GetSessionCategoriesAsync(userId, sessionId, cancellationToken);
        var sessionAffinityByVenue = candidates.ToDictionary(c => c.VenueId, c =>
        {
            if (sessionCategories.Count == 0) return 0.0;
            var target = c.Categories.Select(x => x.ToLowerInvariant()).ToHashSet();
            var overlap = target.Overlaps(sessionCategories);
            return overlap ? 1.0 : 0.0;
        });

        // Base score per venue (without diversity)
        var baseScores = new List<(VenueScoreInput Input, double Score)>();
        foreach (var c in candidates)
        {
            var emb = _weights.EmbeddingSimilarity * c.EmbeddingSimilarity;
            var pop = _weights.Popularity * popularityByVenue.GetValueOrDefault(c.VenueId, 0);
            var rec = _weights.Recency * recencyByVenue.GetValueOrDefault(c.VenueId, 0);
            var sess = _weights.SessionAffinity * sessionAffinityByVenue.GetValueOrDefault(c.VenueId, 0);
            var baseScore = emb + pop + rec + sess;
            baseScores.Add((c, baseScore));
        }

        // Sort by base score
        var ordered = baseScores.OrderByDescending(x => x.Score).ToList();

        // Apply diversity_penalty
        var penaltyApplied = new List<(VenueScoreInput Input, double FinalScore)>();
        string[]? prevCategories = null;
        var sameCategoryStreak = 0;

        foreach (var (input, baseScore) in ordered)
        {
            var cats = input.Categories.Select(x => x.ToLowerInvariant()).ToArray();
            var isSameCategory = prevCategories != null && cats.Length > 0 && prevCategories.Length > 0 &&
                                cats.Any(c => prevCategories.Contains(c));

            double penalty = 0;
            if (isSameCategory)
            {
                sameCategoryStreak++;
                penalty = sameCategoryStreak == 1 ? 0.05 : 0.10; // 2nd in row → -0.05, 3rd → -0.10
            }
            else
            {
                sameCategoryStreak = 0;
            }

            prevCategories = cats.Length > 0 ? cats : null;
            var finalScore = Math.Max(0, baseScore - (_weights.DiversityPenalty * penalty));
            penaltyApplied.Add((input, finalScore));
        }

        // Re-sort by final score, apply offset/limit
        var sessionInfluenced = sessionId.HasValue;
        return penaltyApplied
            .OrderByDescending(x => x.FinalScore)
            .Skip(offset)
            .Take(limit)
            .Select(x => new ScoredVenue(
                x.Input.VenueId,
                x.FinalScore,
                "hybrid",
                sessionInfluenced))
            .ToList();
    }

    private async Task<HashSet<string>> GetSessionCategoriesAsync(Guid userId, Guid? sessionId, CancellationToken ct)
    {
        if (!sessionId.HasValue) return new HashSet<string>();

        var sessionVenueIds = await _interactionRepo.GetVenueIdsBySessionAsync(userId, sessionId.Value, ct);
        if (sessionVenueIds.Count == 0) return new HashSet<string>();

        var allCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var venueId in sessionVenueIds)
        {
            var emb = await _venueEmbeddingRepo.GetByVenueIdAsync(venueId, ct);
            if (emb?.Categories != null)
            {
                foreach (var c in emb.Categories)
                    allCategories.Add(c.ToLowerInvariant());
            }
        }
        return allCategories;
    }
}
