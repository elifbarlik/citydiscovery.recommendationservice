using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Infrastructure.ExternalServices;

/// <summary>
/// VenueService REST API HTTP istemcisi.
///
/// VenueCreated/VenueUpdated event'lerinde eksik olan Categories bilgisini
/// GET /api/venues/{id}/categories endpoint'inden çeker.
///
/// NOT: VenueService'in hiçbir event'i ve endpoint'i CityId içermez.
/// CityId null bırakılır; öneri motoru GetAllAsync fallback'i kullanır.
/// </summary>
public class VenueServiceClient : IVenueServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VenueServiceClient> _logger;

    public VenueServiceClient(HttpClient httpClient, ILogger<VenueServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// VenueService'de GET /api/venues/{id}/address endpoint'i bulunmamaktadır (sadece PUT var).
    /// CityId REST API üzerinden çekilemediğinden her zaman null döner.
    public Task<int?> GetVenueCityIdAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<int?>(null);
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetVenueCategoriesAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/venues/{venueId}/categories", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "[VenueServiceClient] Venue '{VenueId}' not found on VenueService (404). " +
                    "Returning empty categories.", venueId);
                return new List<string>();
            }

            response.EnsureSuccessStatusCode();

            var categories = await response.Content.ReadFromJsonAsync<List<VenueCategoryDto>>(
                cancellationToken: cancellationToken);

            if (categories == null || categories.Count == 0)
                return new List<string>();

            var slugs = categories
                .Where(c => !string.IsNullOrWhiteSpace(c.Slug))
                .Select(c => c.Slug!)
                .ToList();

            _logger.LogInformation(
                "[VenueServiceClient] Fetched {Count} categories for Venue '{VenueId}': [{Categories}]",
                slugs.Count, venueId, string.Join(", ", slugs));

            return slugs;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[VenueServiceClient] HTTP error fetching categories for Venue '{VenueId}'. " +
                "Returning empty categories.", venueId);
            return new List<string>();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex,
                "[VenueServiceClient] Timeout fetching categories for Venue '{VenueId}'. " +
                "Returning empty categories.", venueId);
            return new List<string>();
        }
    }

    // Mirrors VenueService's CategoryDto
    private sealed class VenueCategoryDto
    {
        [JsonPropertyName("categoryId")]
        public int CategoryId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; init; }
    }
}
