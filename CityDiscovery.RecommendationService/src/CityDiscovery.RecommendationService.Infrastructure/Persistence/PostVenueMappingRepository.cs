using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class PostVenueMappingRepository : IPostVenueMappingRepository
{
    private readonly RecommendationDbContext _context;

    public PostVenueMappingRepository(RecommendationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetVenueIdByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var mapping = await _context.PostVenueMappings
            .FirstOrDefaultAsync(m => m.PostId == postId, cancellationToken);
        return mapping?.VenueId;
    }

    public async Task AddAsync(PostVenueMapping mapping, CancellationToken cancellationToken = default)
    {
        var exists = await _context.PostVenueMappings
            .AnyAsync(m => m.PostId == mapping.PostId, cancellationToken);
        if (!exists)
        {
            _context.PostVenueMappings.Add(mapping);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        var mappings = await _context.PostVenueMappings
            .Where(m => m.VenueId == venueId)
            .ToListAsync(cancellationToken);

        if (mappings.Count > 0)
        {
            _context.PostVenueMappings.RemoveRange(mappings);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
