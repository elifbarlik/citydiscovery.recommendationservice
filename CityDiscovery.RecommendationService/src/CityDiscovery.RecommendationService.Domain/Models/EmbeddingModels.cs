using CityDiscovery.RecommendationService.Domain.Constants;

namespace CityDiscovery.RecommendationService.Domain.Models;

public static class VectorSettings
{
    /// <summary>
    /// Fixed dimension for all embeddings in the system.
    /// </summary>
    public const int Dimension = EmbeddingConstants.Dimensions;
}

public record InteractionVector(float[] VenueVector, float Weight);
