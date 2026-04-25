using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class IdempotencyService : IIdempotencyService
{
    private readonly RecommendationDbContext _dbContext;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(RecommendationDbContext dbContext, ILogger<IdempotencyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task RecordAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default)
    {
        var processedEvent = new ProcessedEvent
        {
            EventId = eventId,
            EventType = eventType,
            ProcessedAt = DateTime.UtcNow
        };

        _dbContext.ProcessedEvents.Add(processedEvent);
        
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // If uniqueness constraint violation occurs, it means event was processed concurrently.
            // We treat this as "already recorded" and safe.
            _logger.LogWarning(ex, "Failed to record event {EventId} - likely duplicate.", eventId);
            throw; // Or handle gracefully depending on requirement. 
                   // Given requirement "duplicate delivery safe", we should let check handle it,
                   // but concurrent check might race. Database constraint is the final guard.
        }
    }
}
