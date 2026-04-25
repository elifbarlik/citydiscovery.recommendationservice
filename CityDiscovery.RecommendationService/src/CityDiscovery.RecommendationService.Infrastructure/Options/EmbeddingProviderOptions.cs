namespace CityDiscovery.RecommendationService.Infrastructure.Options;

public class EmbeddingProviderOptions
{
    public bool UseMock { get; set; } = false;
    public GeminiOptions Gemini { get; set; } = new();

    public class GeminiOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-embedding-001";
        public int Dimensions { get; set; } = CityDiscovery.RecommendationService.Domain.Constants.EmbeddingConstants.Dimensions;
    }
}
