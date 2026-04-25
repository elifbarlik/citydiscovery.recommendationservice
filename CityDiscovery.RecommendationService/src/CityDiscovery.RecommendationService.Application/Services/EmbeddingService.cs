using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Domain.Models;
using CityDiscovery.RecommendationService.Domain.Constants;

using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingProvider _provider;

    public EmbeddingService(IEmbeddingProvider provider, ILogger<EmbeddingService> logger)
    {
        _provider = provider;
        logger.LogInformation("Resolved embedding provider: {ProviderType}", provider.GetType().Name);
        
        // Fail Fast if provider dimension doesn't match system settings
        if (_provider.Dimension != EmbeddingConstants.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding provider dimension mismatch. System requires {EmbeddingConstants.Dimensions}, but provider offers {_provider.Dimension}.");
        }
    }

    public async Task<float[]> GetVenueEmbeddingAsync(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new float[EmbeddingConstants.Dimensions];
        }

        var vector = await _provider.GetVectorAsync(normalizedText);

        // Validation
        if (vector.Length != EmbeddingConstants.Dimensions)
        {
            throw new InvalidOperationException(
                $"Provider returned vector of length {vector.Length}, expected {EmbeddingConstants.Dimensions}.");
        }

        return vector;
    }

    public float[] ComputeUserEmbedding(IEnumerable<InteractionVector> interactions)
    {
        if (interactions == null || !interactions.Any())
        {
            return new float[EmbeddingConstants.Dimensions];
        }

        var sumVector = new float[EmbeddingConstants.Dimensions];
        bool hasValues = false;

        foreach (var interaction in interactions)
        {
            // Dimension enforcement for each input vector
            if (interaction.VenueVector.Length != EmbeddingConstants.Dimensions)
            {
                throw new ArgumentException(
                    $"Interaction venue vector dimension mismatch. Expected {EmbeddingConstants.Dimensions}.", 
                    nameof(interactions));
            }

            for (int i = 0; i < EmbeddingConstants.Dimensions; i++)
            {
                sumVector[i] += interaction.Weight * interaction.VenueVector[i];
                if (sumVector[i] != 0) hasValues = true;
            }
        }

        if (!hasValues)
        {
            return new float[EmbeddingConstants.Dimensions];
        }

        return L2Normalize(sumVector);
    }

    private float[] L2Normalize(float[] vector)
    {
        double sumSquares = 0;
        for (int i = 0; i < EmbeddingConstants.Dimensions; i++)
        {
            sumSquares += (double)vector[i] * vector[i];
        }

        float norm = (float)Math.Sqrt(sumSquares);

        // Safety: Zero-norm check
        if (norm < 1e-10f)
        {
            return new float[EmbeddingConstants.Dimensions];
        }

        var normalized = new float[EmbeddingConstants.Dimensions];
        for (int i = 0; i < EmbeddingConstants.Dimensions; i++)
        {
            normalized[i] = vector[i] / norm;
        }

        return normalized;
    }
}
