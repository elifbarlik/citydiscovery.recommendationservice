using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Infrastructure.Options;
using Microsoft.Extensions.Options;
using CityDiscovery.RecommendationService.Domain.Constants;

namespace CityDiscovery.RecommendationService.Infrastructure.Persistence;

public class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingProviderOptions _options;

    public GeminiEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<EmbeddingProviderOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("Gemini");
        _options = options.Value;
    }

    public int Dimension => EmbeddingConstants.Dimensions;

    public async Task<float[]> GetVectorAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[Dimension];
        }

        if (string.IsNullOrWhiteSpace(_options.Gemini.Model))
        {
            throw new InvalidOperationException("Gemini model name is not configured in EmbeddingProvider:Gemini:Model.");
        }

        var request = new GeminiEmbeddingRequest
        {
            Model = $"models/{_options.Gemini.Model}",
            Content = new GeminiContent
            {
                Parts = new[] { new GeminiPart { Text = text } }
            },
            OutputDimensionality = EmbeddingConstants.Dimensions
        };

        // Query param key usage - text-embedding-004 requires v1beta
        var url = $"v1beta/models/{_options.Gemini.Model}:embedContent?key={_options.Gemini.ApiKey}";
        var response = await _httpClient.PostAsJsonAsync(url, request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Gemini API error ({response.StatusCode}) for URL {url}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>();

        if (result?.Embedding?.Values == null || result.Embedding.Values.Length == 0)
        {
            throw new InvalidOperationException("Gemini returned an empty embedding response.");
        }

        return result.Embedding.Values;
    }

    private class GeminiEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; } = new();

        [JsonPropertyName("outputDimensionality")]
        public int OutputDimensionality { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class GeminiEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbeddingValues Embedding { get; set; } = new();
    }

    private class GeminiEmbeddingValues
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}
