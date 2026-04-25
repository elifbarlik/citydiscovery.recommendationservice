using CityDiscovery.RecommendationService.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class InteractionWeightProviderTests
{
    private readonly InteractionWeightProvider _sut;

    public InteractionWeightProviderTests()
    {
        var logger = new Mock<ILogger<InteractionWeightProvider>>();
        _sut = new InteractionWeightProvider(logger.Object);
    }

    [Fact]
    public void GetWeight_Like_Returns1()
    {
        _sut.GetWeight("like").Should().Be(1.0);
    }

    [Fact]
    public void GetWeight_Save_Returns2()
    {
        _sut.GetWeight("save").Should().Be(2.0);
    }

    [Fact]
    public void GetWeight_Favorite_Returns3()
    {
        _sut.GetWeight("favorite").Should().Be(3.0);
    }

    [Theory]
    [InlineData(5.0, 2.5)]  // 2.5 * (5/5) = 2.5
    [InlineData(4.0, 2.0)]  // 2.5 * (4/5) = 2.0
    [InlineData(3.0, 1.5)]  // 2.5 * (3/5) = 1.5
    [InlineData(1.0, 0.5)]  // 2.5 * (1/5) = 0.5
    public void GetWeight_Review_ReturnsRatingBasedWeight(double rating, double expected)
    {
        _sut.GetWeight("review", rating).Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void GetWeight_ReviewSubmitted_AlsoWorks()
    {
        _sut.GetWeight("review.submitted", 5.0).Should().BeApproximately(2.5, 0.001);
    }

    [Fact]
    public void GetWeight_Review_WithoutRating_Returns1()
    {
        _sut.GetWeight("review").Should().Be(1.0);
    }

    [Fact]
    public void GetWeight_View_LongDuration_Returns03()
    {
        // ViewDuration >= 10 seconds → 0.3
        _sut.GetWeight("view", 15.0).Should().Be(0.3);
    }

    [Fact]
    public void GetWeight_View_ShortDuration_Returns01()
    {
        // ViewDuration < 10 seconds → 0.1
        _sut.GetWeight("view", 5.0).Should().Be(0.1);
    }

    [Fact]
    public void GetWeight_View_ExactlyTenSeconds_Returns03()
    {
        _sut.GetWeight("view", 10.0).Should().Be(0.3);
    }

    [Fact]
    public void GetWeight_View_NoDuration_Returns01()
    {
        // No duration → defaults to 0 seconds → < 10 → 0.1
        _sut.GetWeight("view").Should().Be(0.1);
    }

    [Fact]
    public void GetWeight_Unknown_Returns1()
    {
        _sut.GetWeight("unknown_type").Should().Be(1.0);
    }

    [Fact]
    public void GetWeight_CaseInsensitive()
    {
        _sut.GetWeight("LIKE").Should().Be(1.0);
        _sut.GetWeight("Save").Should().Be(2.0);
        _sut.GetWeight("FAVORITE").Should().Be(3.0);
        _sut.GetWeight("VIEW", 15.0).Should().Be(0.3);
    }
}
