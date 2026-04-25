using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class DismissedVenueRepository : IDismissedVenueRepository
{
    private readonly RecommendationDbContext _context;

    public DismissedVenueRepository(RecommendationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Guid>> GetDismissedVenueIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.DismissedVenues
            .Where(d => d.UserId == userId)
            .Select(d => d.VenueId)
            .ToListAsync(cancellationToken);
    }

    public async Task DismissAsync(Guid userId, Guid venueId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.DismissedVenues
            .AnyAsync(d => d.UserId == userId && d.VenueId == venueId, cancellationToken);

        if (!exists)
        {
            _context.DismissedVenues.Add(new DismissedVenue
            {
                UserId = userId,
                VenueId = venueId,
                DismissedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UndismissAsync(Guid userId, Guid venueId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.DismissedVenues
            .FirstOrDefaultAsync(d => d.UserId == userId && d.VenueId == venueId, cancellationToken);

        if (entity != null)
        {
            _context.DismissedVenues.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.DismissedVenues
            .Where(d => d.VenueId == venueId)
            .ToListAsync(cancellationToken);

        if (entities.Count > 0)
        {
            _context.DismissedVenues.RemoveRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.DismissedVenues
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);

        if (entities.Count > 0)
        {
            _context.DismissedVenues.RemoveRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
