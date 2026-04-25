using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CityDiscovery.RecommendationService.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _sessionTtl;
    private const string KeyPrefix = "session:";

    public SessionService(IMemoryCache cache, IOptions<SessionServiceOptions> options)
    {
        _cache = cache;
        _sessionTtl = TimeSpan.FromMinutes(options.Value.SessionTtlMinutes);
    }

    public Guid StartSession(Guid userId)
    {
        var sessionId = Guid.NewGuid();
        var key = KeyPrefix + userId;
        _cache.Set(key, sessionId, _sessionTtl);
        return sessionId;
    }

    public Guid? GetActiveSession(Guid userId)
    {
        var key = KeyPrefix + userId;
        return _cache.TryGetValue<Guid>(key, out var sessionId) ? sessionId : null;
    }

    public void EndSession(Guid userId)
    {
        var key = KeyPrefix + userId;
        _cache.Remove(key);
    }
}

public class SessionServiceOptions
{
    public const string SectionName = "Session";

    /// <summary>
    /// Session TTL in minutes. Default 30.
    /// </summary>
    public int SessionTtlMinutes { get; set; } = 30;
}
