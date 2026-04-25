namespace CityDiscovery.RecommendationService.Domain.Entities;

/// <summary>
/// SocialService'den gelen PostCreatedEvent'teki PostId→VenueId eşlemesini saklar.
/// PostLikedEvent geldiğinde VenueId'yi bu tablodan lookup ederiz.
/// </summary>
public class PostVenueMapping
{
    public Guid PostId { get; set; }
    public Guid VenueId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
