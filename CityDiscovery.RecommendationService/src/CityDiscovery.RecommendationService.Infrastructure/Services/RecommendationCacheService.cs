using System.Collections.Concurrent;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CityDiscovery.RecommendationService.Infrastructure.Services;

public class RecommendationCacheService : IRecommendationCacheService, IRecommendationCacheInvalidator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<Guid, int> _userGenerations = new();

    private static readonly TimeSpan SessionActiveTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan NoSessionTtl = TimeSpan.FromMinutes(15);

    public RecommendationCacheService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    public async Task<RecommendationResult?> GetOrComputeAsync(
        Guid userId,
        int cityId,
        int limit,
        int offset,
        Guid? sessionId,
        bool hasActiveSession,
        List<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var gen = _userGenerations.GetOrAdd(userId, _ => 0);
        var sessionKey = sessionId?.ToString("N") ?? "nosession";
        var catKey = categories != null && categories.Count > 0 
            ? string.Join(",", categories.OrderBy(c => c).Select(c => c.ToLowerInvariant())) 
            : "nocat";
        var cacheKey = $"rec_{userId:N}_{cityId}_{sessionKey}_{catKey}_{gen}";

        if (_cache.TryGetValue(cacheKey, out var cached) && cached is RecommendationResult cachedResult)
            return cachedResult;

        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IRecommendationEngine>();
        var computed = await engine.GetRecommendationsAsync(userId, cityId, limit, offset, sessionId, categories, cancellationToken);
        var ttl = hasActiveSession ? SessionActiveTtl : NoSessionTtl;
        _cache.Set(cacheKey, computed, ttl);
        return computed;
    }

    public void InvalidateForUser(Guid userId)
    {
        _userGenerations.AddOrUpdate(userId, 1, (_, v) => v + 1);
    }
}
