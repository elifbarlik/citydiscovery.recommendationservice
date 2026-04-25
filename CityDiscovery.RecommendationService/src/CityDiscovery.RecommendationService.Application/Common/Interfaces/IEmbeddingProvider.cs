namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IEmbeddingProvider
{
    /// <summary>
    /// Computes a vector for the given text.
    /// </summary>
    Task<float[]> GetVectorAsync(string text);

    /// <summary>
    /// The dimension output by this provider. Must match the system's VectorSettings.
    /// </summary>
    int Dimension { get; }
}
