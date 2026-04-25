using CityDiscovery.RecommendationService.Domain.Models;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Computes hybrid recommendation scores from multiple signals.
/// </summary>
public interface IHybridScoringService
{
    /// <summary>
    /// Computes hybrid scores for candidate venues and returns them ordered by final score.
    /// </summary>
    Task<IReadOnlyList<ScoredVenue>> ComputeHybridScoresAsync(
        IReadOnlyList<VenueScoreInput> candidates,
        Guid userId,
        Guid? sessionId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input for a single venue's scoring.
/// </summary>
public record VenueScoreInput(
    Guid VenueId,
    double EmbeddingSimilarity,
    IReadOnlyList<string> Categories);
