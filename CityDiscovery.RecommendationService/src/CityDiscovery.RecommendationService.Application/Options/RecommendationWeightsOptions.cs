namespace CityDiscovery.RecommendationService.Application.Options;

public class RecommendationWeightsOptions
{
    public const string SectionName = "RecommendationWeights";

    public double EmbeddingSimilarity { get; set; } = 0.5;
    public double Popularity { get; set; } = 0.2;
    public double Recency { get; set; } = 0.15;
    public double SessionAffinity { get; set; } = 0.1;
    public double DiversityPenalty { get; set; } = 0.05;
}
