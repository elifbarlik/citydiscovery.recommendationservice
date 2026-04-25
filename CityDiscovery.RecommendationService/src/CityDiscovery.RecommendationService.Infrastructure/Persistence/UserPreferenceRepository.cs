using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class UserPreferenceRepository : IUserPreferenceRepository
{
    private readonly RecommendationDbContext _context;

    public UserPreferenceRepository(RecommendationDbContext context)
    {
        _context = context;
    }

    public async Task<UserPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task UpsertAsync(UserPreference preference, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == preference.UserId, cancellationToken);

        if (existing == null)
        {
            _context.UserPreferences.Add(preference);
        }
        else
        {
            existing.PreferredCategories = preference.PreferredCategories;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (entity != null)
        {
            _context.UserPreferences.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
