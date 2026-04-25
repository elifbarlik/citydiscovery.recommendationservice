using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Services;

public class InteractionWeightProvider : IInteractionWeightProvider
{
    private readonly ILogger<InteractionWeightProvider> _logger;

    public InteractionWeightProvider(ILogger<InteractionWeightProvider> logger)
    {
        _logger = logger;
    }

    public double GetWeight(string interactionType, double? rating = null)
    {
        // Centralized Weight Configuration
        // In the future, this could be loaded from a database or configuration file.
        
        switch (interactionType.ToLowerInvariant())
        {
            case "post":
                return 0.5;
            case "like":
                return 1.0;
            case "save":
                return 2.0;
            case "favorite":
                return 3.0;
            case "review":
            case "review.submitted": // Handle event type name variation
                if (!rating.HasValue)
                {
                    _logger.LogWarning("Review interaction missing rating. Defaulting to 1.0.");
                    return 1.0; // Fallback
                }
                // Requirement: 2.5 * (rating / 5)
                return 2.5 * (rating.Value / 5.0);
                
            case "view":
                // rating parameter carries ViewDuration in seconds
                var durationSeconds = rating ?? 0;
                return durationSeconds >= 10 ? 0.3 : 0.1;
                
            default:
                _logger.LogWarning("Unknown interaction type '{InteractionType}'. Defaulting to 1.0.", interactionType);
                return 1.0;
        }
    }
}
