namespace CityDiscovery.RecommendationService.Application.Common.Interfaces;

/// <summary>
/// VenueService REST API ile iletişim kurar.
/// VenueCreated/VenueUpdated event'lerinde eksik olan
/// Categories ve CityId bilgilerini HTTP üzerinden çeker.
/// </summary>
public interface IVenueServiceClient
{
    /// <summary>
    /// Venue'nun CityId bilgisini döndürür (VenueService Address üzerinden, int tipinde).
    /// Bulunamazsa null döner.
    /// </summary>
    Task<int?> GetVenueCityIdAsync(Guid venueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Venue'nun kategori slug listesini döndürür.
    /// Bulunamazsa boş liste döner.
    /// </summary>
    Task<List<string>> GetVenueCategoriesAsync(Guid venueId, CancellationToken cancellationToken = default);
}
