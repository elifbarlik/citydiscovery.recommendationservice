namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

public interface IInteractionWeightProvider
{
    /// <summary>
    /// Calculates the weight for a given interaction type and optional parameters.
    /// </summary>
    /// <param name="interactionType">Currently supported: Like, Save, Favorite, Review</param>
    /// <param name="rating">Optional rating (1-5) required for Review type</param>
    /// <returns>The calculated weight</returns>
    double GetWeight(string interactionType, double? rating = null);
}
