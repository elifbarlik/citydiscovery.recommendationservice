namespace CityDiscovery.RecommendationService.Domain.Constants;

public static class EmbeddingConstants
{
    /// <summary>
    /// Fixed dimension for all embeddings in the system.
    /// Google Gemini "gemini-embedding-001" model supports this via outputDimensionality.
    /// </summary>
    public const int Dimensions = 384;
}
