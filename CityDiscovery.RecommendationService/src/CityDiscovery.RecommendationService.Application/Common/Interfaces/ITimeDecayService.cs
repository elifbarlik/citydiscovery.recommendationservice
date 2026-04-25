namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Calculates time-decayed weights for user interactions.
/// Combines global exponential decay with session-based boosts.
/// </summary>
public interface ITimeDecayService
{
    /// <summary>
    /// Computes the final weight for an interaction at a given time.
    /// Formula: (base_weight × global_decay) + session_boost
    /// </summary>
    /// <param name="baseWeight">Base weight of the interaction type (e.g. from IInteractionWeightProvider).</param>
    /// <param name="occurredAt">When the interaction occurred.</param>
    /// <param name="sessionId">Session ID of the interaction (Guid.Empty if none).</param>
    /// <param name="referenceTime">Time to compute decay against (default: UtcNow).</param>
    /// <param name="activeSessionId">User's currently active session (for session boost logic).</param>
    double ComputeWeight(
        double baseWeight,
        DateTime occurredAt,
        Guid sessionId,
        DateTime? referenceTime = null,
        Guid? activeSessionId = null);
}
