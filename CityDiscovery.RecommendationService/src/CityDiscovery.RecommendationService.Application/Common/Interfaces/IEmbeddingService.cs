using CityDiscovery.RecommendationService.Domain.Models;

namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IEmbeddingService
{
    /// <summary>
    /// Gets a deterministic embedding for a venue's normalized feature text.
    /// </summary>
    Task<float[]> GetVenueEmbeddingAsync(string normalizedText);

    /// <summary>
    /// Computes a user embedding from weighted venue interactions using L2 normalization.
    /// </summary>
    float[] ComputeUserEmbedding(IEnumerable<InteractionVector> interactions);
}
