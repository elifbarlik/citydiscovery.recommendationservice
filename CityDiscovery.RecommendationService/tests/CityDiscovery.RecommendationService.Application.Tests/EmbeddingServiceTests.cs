using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Application.Services;
using CityDiscovery.RecommendationService.Domain.Constants;
using CityDiscovery.RecommendationService.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class EmbeddingServiceTests
{
    private Mock<IEmbeddingProvider> CreateMockProvider(int dimension = EmbeddingConstants.Dimensions)
    {
        var mock = new Mock<IEmbeddingProvider>();
        mock.Setup(p => p.Dimension).Returns(dimension);
        return mock;
    }

    private EmbeddingService CreateService(Mock<IEmbeddingProvider> provider)
    {
        var logger = new Mock<ILogger<EmbeddingService>>();
        return new EmbeddingService(provider.Object, logger.Object);
    }

    [Fact]
    public void Constructor_DimensionMismatch_ThrowsException()
    {
        var provider = CreateMockProvider(dimension: 256); // yanlış boyut
        var logger = new Mock<ILogger<EmbeddingService>>();

        var act = () => new EmbeddingService(provider.Object, logger.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*dimension mismatch*");
    }

    [Fact]
    public async Task GetVenueEmbeddingAsync_EmptyText_ReturnsZeroVector()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var result = await sut.GetVenueEmbeddingAsync("");

        result.Should().HaveCount(EmbeddingConstants.Dimensions);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GetVenueEmbeddingAsync_WhitespaceText_ReturnsZeroVector()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var result = await sut.GetVenueEmbeddingAsync("   ");

        result.Should().HaveCount(EmbeddingConstants.Dimensions);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GetVenueEmbeddingAsync_ValidText_ReturnsProviderVector()
    {
        var provider = CreateMockProvider();
        var expectedVector = new float[EmbeddingConstants.Dimensions];
        expectedVector[0] = 0.5f;
        expectedVector[1] = 0.3f;

        provider.Setup(p => p.GetVectorAsync("test text"))
            .ReturnsAsync(expectedVector);

        var sut = CreateService(provider);
        var result = await sut.GetVenueEmbeddingAsync("test text");

        result.Should().Equal(expectedVector);
    }

    [Fact]
    public async Task GetVenueEmbeddingAsync_WrongDimensionFromProvider_ThrowsException()
    {
        var provider = CreateMockProvider();
        var wrongVector = new float[100]; // yanlış boyut
        provider.Setup(p => p.GetVectorAsync(It.IsAny<string>()))
            .ReturnsAsync(wrongVector);

        var sut = CreateService(provider);
        var act = () => sut.GetVenueEmbeddingAsync("some text");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*length*");
    }

    [Fact]
    public void ComputeUserEmbedding_EmptyInteractions_ReturnsZeroVector()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var result = sut.ComputeUserEmbedding(Enumerable.Empty<InteractionVector>());

        result.Should().HaveCount(EmbeddingConstants.Dimensions);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void ComputeUserEmbedding_NullInteractions_ReturnsZeroVector()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var result = sut.ComputeUserEmbedding(null!);

        result.Should().HaveCount(EmbeddingConstants.Dimensions);
    }

    [Fact]
    public void ComputeUserEmbedding_SingleInteraction_ReturnsNormalizedVector()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var vector = new float[EmbeddingConstants.Dimensions];
        vector[0] = 3f;
        vector[1] = 4f;

        var interactions = new[] { new InteractionVector(vector, 1.0f) };
        var result = sut.ComputeUserEmbedding(interactions);

        // L2 norm of [3,4,0,...] = 5; normalized: [0.6, 0.8, 0, ...]
        result[0].Should().BeApproximately(0.6f, 0.01f);
        result[1].Should().BeApproximately(0.8f, 0.01f);
    }

    [Fact]
    public void ComputeUserEmbedding_WeightedSum_Applied()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var v1 = new float[EmbeddingConstants.Dimensions];
        v1[0] = 1f;
        var v2 = new float[EmbeddingConstants.Dimensions];
        v2[0] = 1f;

        var interactions = new[]
        {
            new InteractionVector(v1, 2.0f), // ağırlık 2
            new InteractionVector(v2, 1.0f)  // ağırlık 1
        };

        var result = sut.ComputeUserEmbedding(interactions);

        // Sum: [3, 0, ...], L2 normalized: [1.0, 0, ...]
        result[0].Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public void ComputeUserEmbedding_WrongDimensionVector_ThrowsException()
    {
        var provider = CreateMockProvider();
        var sut = CreateService(provider);

        var wrongVector = new float[100]; // boyut uyuşmazlığı
        wrongVector[0] = 1f;

        var interactions = new[] { new InteractionVector(wrongVector, 1.0f) };
        var act = () => sut.ComputeUserEmbedding(interactions);

        act.Should().Throw<ArgumentException>();
    }
}
