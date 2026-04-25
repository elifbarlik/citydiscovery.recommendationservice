namespace CityDiscovery.RecommendationService.Domain.Models;

public record VenueProfile(
    string? Description = null,
    List<string>? Categories = null,
    List<MenuItem>? MenuItems = null,
    List<VenueEvent>? Events = null,
    string? ReviewSummary = null
);

public record MenuItem(string Name, string? Category = null);

public record VenueEvent(string Title);
