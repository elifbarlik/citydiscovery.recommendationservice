using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IUserProfileRepository backed by SQL Server.
/// </summary>
public class UserProfileRepository : IUserProfileRepository
{
    private readonly RecommendationDbContext _context;

    public UserProfileRepository(RecommendationDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == profile.UserId, cancellationToken);

        if (existing == null)
        {
            _context.UserProfiles.Add(profile);
        }
        else
        {
            existing.Embedding = profile.Embedding;
            existing.LastUpdatedAt = profile.LastUpdatedAt;
            existing.ActiveSessionId = profile.ActiveSessionId;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (entity != null)
        {
            _context.UserProfiles.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
