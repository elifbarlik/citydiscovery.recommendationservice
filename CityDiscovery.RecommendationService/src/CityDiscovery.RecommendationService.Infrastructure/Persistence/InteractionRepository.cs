using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class InteractionRepository : IInteractionRepository
{
    private readonly RecommendationDbContext _context;

    public InteractionRepository(RecommendationDbContext context)
    {
        _context = context;
    }

    public async Task LogInteractionAsync(InteractionLog log, CancellationToken cancellationToken = default)
    {
        _context.InteractionLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InteractionLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.InteractionLogs
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetInteractedVenueIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.InteractionLogs
            .Where(i => i.UserId == userId)
            .Select(i => i.VenueId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetMostInteractedVenueIdsAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.InteractionLogs
            .GroupBy(i => i.VenueId)
            .OrderByDescending(g => g.Count())
            .Take(limit)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetInteractionCountsByVenueIdsAsync(IEnumerable<Guid> venueIds, CancellationToken cancellationToken = default)
    {
        var idSet = venueIds.ToHashSet();
        var counts = await _context.InteractionLogs
            .Where(i => idSet.Contains(i.VenueId))
            .GroupBy(i => i.VenueId)
            .Select(g => new { VenueId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return counts.ToDictionary(x => x.VenueId, x => x.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetLastInteractionDatesByVenueIdsAsync(IEnumerable<Guid> venueIds, CancellationToken cancellationToken = default)
    {
        var idSet = venueIds.ToHashSet();
        var dates = await _context.InteractionLogs
            .Where(i => idSet.Contains(i.VenueId))
            .GroupBy(i => i.VenueId)
            .Select(g => new { VenueId = g.Key, LastDate = g.Max(i => i.Timestamp) })
            .ToListAsync(cancellationToken);
        return dates.ToDictionary(x => x.VenueId, x => x.LastDate);
    }

    public async Task<IReadOnlyList<Guid>> GetVenueIdsBySessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.InteractionLogs
            .Where(i => i.UserId == userId && i.SessionId == sessionId)
            .Select(i => i.VenueId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetTrendingVenueIdsAsync(int cityId, int days, int limit, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        // Join with VenueEmbeddings to filter by CityId
        var trending = await (
            from i in _context.InteractionLogs
            join v in _context.VenueEmbeddings on i.VenueId equals v.VenueId
            where i.Timestamp >= cutoff && v.CityId == cityId
            group i by i.VenueId into g
            orderby g.Count() descending
            select g.Key
        ).Take(limit).ToListAsync(cancellationToken);

        return trending;
    }

    public async Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        var logs = await _context.InteractionLogs
            .Where(i => i.VenueId == venueId)
            .ToListAsync(cancellationToken);

        if (logs.Count > 0)
        {
            _context.InteractionLogs.RemoveRange(logs);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var logs = await _context.InteractionLogs
            .Where(i => i.UserId == userId)
            .ToListAsync(cancellationToken);

        if (logs.Count > 0)
        {
            _context.InteractionLogs.RemoveRange(logs);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
