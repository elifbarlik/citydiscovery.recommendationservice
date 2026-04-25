using CityDiscovery.RecommendationService.Application.Services;
using CityDiscovery.RecommendationService.Domain.Models;
using FluentAssertions;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class VenueFeatureExtractorTests
{
    private readonly VenueFeatureExtractor _sut = new();

    [Fact]
    public void ExtractFeatureText_FullProfile_CombinesAllFields()
    {
        var profile = new VenueProfile(
            Description: "A cozy Italian restaurant",
            Categories: new List<string> { "Restaurant", "Italian" },
            MenuItems: new List<MenuItem> { new("Pasta Carbonara"), new("Pizza Margherita") },
            Events: new List<VenueEvent> { new("Live Jazz Night") }
        );

        var result = _sut.ExtractFeatureText(profile);

        result.Should().Contain("cozy italian restaurant");
        result.Should().Contain("restaurant, italian");
        result.Should().Contain("pasta carbonara");
        result.Should().Contain("live jazz night");
    }

    [Fact]
    public void ExtractFeatureText_HtmlInDescription_Stripped()
    {
        var profile = new VenueProfile(
            Description: "<p>Best <strong>coffee</strong> in town!</p>"
        );

        var result = _sut.ExtractFeatureText(profile);

        result.Should().NotContain("<p>");
        result.Should().NotContain("<strong>");
        result.Should().Contain("best coffee in town");
    }

    [Fact]
    public void ExtractFeatureText_SpecialCharacters_Removed()
    {
        var profile = new VenueProfile(
            Description: "Great café! ★★★★★ #1 choice"
        );

        var result = _sut.ExtractFeatureText(profile);

        result.Should().NotContain("★");
        result.Should().NotContain("#");
        result.Should().Contain("great caf");
    }

    [Fact]
    public void ExtractFeatureText_EmptyProfile_ReturnsEmpty()
    {
        var profile = new VenueProfile();

        var result = _sut.ExtractFeatureText(profile);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFeatureText_NullFields_ReturnsEmpty()
    {
        var profile = new VenueProfile(null, null, null, null, null);

        var result = _sut.ExtractFeatureText(profile);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFeatureText_OnlyDescription_Works()
    {
        var profile = new VenueProfile(Description: "Simple venue");

        var result = _sut.ExtractFeatureText(profile);

        result.Should().Be("simple venue");
    }

    [Fact]
    public void ExtractFeatureText_OnlyCategories_Works()
    {
        var profile = new VenueProfile(Categories: new List<string> { "Cafe", "Bakery" });

        var result = _sut.ExtractFeatureText(profile);

        result.Should().Be("cafe, bakery");
    }

    [Fact]
    public void ExtractFeatureText_MultipleSpaces_Collapsed()
    {
        var profile = new VenueProfile(Description: "Too    many     spaces");

        var result = _sut.ExtractFeatureText(profile);

        result.Should().NotContain("  ");
    }

    [Fact]
    public void ExtractFeatureText_Lowercase_Applied()
    {
        var profile = new VenueProfile(Description: "UPPERCASE TEXT");

        var result = _sut.ExtractFeatureText(profile);

        result.Should().Be("uppercase text");
    }
}
