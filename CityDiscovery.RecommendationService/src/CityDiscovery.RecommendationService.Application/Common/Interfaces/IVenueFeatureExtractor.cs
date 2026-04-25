using CityDiscovery.RecommendationService.Domain.Models;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IVenueFeatureExtractor
{
    /// <summary>
    /// Transforms a structured VenueProfile into a single normalized text blob for embeddings.
    /// </summary>
    string ExtractFeatureText(VenueProfile profile);
}
