using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IVenueEmbeddingRepository"/>.
/// </summary>
public class VenueEmbeddingRepository : IVenueEmbeddingRepository
{
    private readonly RecommendationDbContext _context;

    public VenueEmbeddingRepository(RecommendationDbContext context)
    {
        _context = context;
    }

    public async Task<VenueEmbedding?> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        return await _context.VenueEmbeddings
            .FirstOrDefaultAsync(e => e.VenueId == venueId, cancellationToken);
    }

    public async Task<IReadOnlyList<VenueEmbedding>> GetAllByCityIdAsync(int cityId, CancellationToken cancellationToken = default)
    {
        return await _context.VenueEmbeddings
            .Where(e => e.CityId == cityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VenueEmbedding>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VenueEmbeddings.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(VenueEmbedding venueEmbedding, CancellationToken cancellationToken = default)
    {
        _context.VenueEmbeddings.Add(venueEmbedding);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(VenueEmbedding venueEmbedding, CancellationToken cancellationToken = default)
    {
        venueEmbedding.UpdatedAt = DateTime.UtcNow;
        _context.VenueEmbeddings.Update(venueEmbedding);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        var embedding = await _context.VenueEmbeddings
            .FirstOrDefaultAsync(e => e.VenueId == venueId, cancellationToken);

        if (embedding != null)
        {
            _context.VenueEmbeddings.Remove(embedding);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
