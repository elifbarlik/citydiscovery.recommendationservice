using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Application.Options;
using Microsoft.Extensions.Options;

namespace CityDiscovery.RecommendationService.Application.Services;

public class TimeDecayService : ITimeDecayService
{
    private readonly TimeDecayOptions _options;

    public TimeDecayService(IOptions<TimeDecayOptions> options)
    {
        _options = options.Value;
    }

    public double ComputeWeight(
        double baseWeight,
        DateTime occurredAt,
        Guid sessionId,
        DateTime? referenceTime = null,
        Guid? activeSessionId = null)
    {
        var now = referenceTime ?? DateTime.UtcNow;

        // a) Global decay: weight = base_weight × e^(-λ × days)
        var daysSince = (now - occurredAt).TotalDays;
        var decay = Math.Exp(-_options.Lambda * daysSince);
        var decayedWeight = baseWeight * decay;

        // b) Session boost
        double sessionBoost = 0;

        if (sessionId != Guid.Empty && activeSessionId.HasValue && sessionId == activeSessionId.Value)
        {
            var minutesSince = (now - occurredAt).TotalMinutes;
            if (minutesSince <= _options.SessionWindowMinutes)
                sessionBoost = _options.ActiveSessionBoost;
            else
                sessionBoost = _options.RecentSessionBoost;
        }

        return decayedWeight + sessionBoost;
    }
}
