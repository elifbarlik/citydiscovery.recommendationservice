namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// Manages user sessions for recommendation context.
/// Session state is stored in memory cache with configurable TTL.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Starts a new session for the user and returns the session ID.
    /// </summary>
    Guid StartSession(Guid userId);

    /// <summary>
    /// Gets the active session ID for the user, or null if none.
    /// </summary>
    Guid? GetActiveSession(Guid userId);

    /// <summary>
    /// Ends the active session for the user.
    /// </summary>
    void EndSession(Guid userId);
}
