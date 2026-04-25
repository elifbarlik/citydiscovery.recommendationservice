using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Constants;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class MockEmbeddingProvider : IEmbeddingProvider
{
    public int Dimension => EmbeddingConstants.Dimensions;

    public Task<float[]> GetVectorAsync(string text)
    {
        // Produce a deterministic vector based on text hash
        var vector = new float[Dimension];
        int seed = text.GetHashCode();
        var random = new Random(seed);

        for (int i = 0; i < Dimension; i++)
        {
            vector[i] = (float)random.NextDouble() * 2 - 1; // Range [-1, 1]
        }

        // Return a unit vector for consistency if needed, 
        // though provider isn't strictly required to be normalized
        return Task.FromResult(vector);
    }
}
