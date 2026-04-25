using CityDiscovery.RecommendationService.Application.Options;
using CityDiscovery.RecommendationService.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CityDiscovery.RecommendationService.Application.Tests;

public class TimeDecayServiceTests
{
    private readonly TimeDecayOptions _options = new()
    {
        Lambda = 0.01,
        SessionWindowMinutes = 30,
        ActiveSessionBoost = 0.5,
        RecentSessionBoost = 0.2
    };

    private TimeDecayService CreateService(TimeDecayOptions? options = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? _options);
        return new TimeDecayService(opts);
    }

    [Fact]
    public void ComputeWeight_RecentInteraction_MinimalDecay()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;
        var occurredAt = now.AddHours(-1); // 1 saat önce

        var result = sut.ComputeWeight(1.0, occurredAt, Guid.Empty, now);

        // e^(-0.01 * 1/24) ≈ 0.9996 → çok az decay
        result.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void ComputeWeight_OldInteraction_SignificantDecay()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;
        var occurredAt = now.AddDays(-100); // 100 gün önce

        var result = sut.ComputeWeight(1.0, occurredAt, Guid.Empty, now);

        // e^(-0.01 * 100) ≈ 0.368
        result.Should().BeApproximately(0.368, 0.01);
    }

    [Fact]
    public void ComputeWeight_WithActiveSessionBoost_AddsBoost()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var occurredAt = now.AddMinutes(-10); // 10 dk önce (< 30 dk window)

        var result = sut.ComputeWeight(1.0, occurredAt, sessionId, now, sessionId);

        // decay ≈ 1.0 (çok yakın), boost = 0.5
        result.Should().BeGreaterThan(1.4);
    }

    [Fact]
    public void ComputeWeight_WithRecentSessionBoost_AddsLessBoost()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var occurredAt = now.AddMinutes(-60); // 60 dk önce (> 30 dk window)

        var result = sut.ComputeWeight(1.0, occurredAt, sessionId, now, sessionId);

        // decay ≈ 1.0, boost = 0.2 (recent, not active)
        result.Should().BeGreaterThan(1.1);
        result.Should().BeLessThan(1.3);
    }

    [Fact]
    public void ComputeWeight_DifferentSession_NoBoost()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var activeSessionId = Guid.NewGuid(); // farklı session

        var result = sut.ComputeWeight(1.0, now, sessionId, now, activeSessionId);

        // No session boost, only decay (which is 1.0 for 0 days)
        result.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void ComputeWeight_EmptySessionId_NoBoost()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;

        var result = sut.ComputeWeight(2.0, now, Guid.Empty, now, Guid.NewGuid());

        result.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public void ComputeWeight_NullReferenceTime_UsesUtcNow()
    {
        var sut = CreateService();
        var occurredAt = DateTime.UtcNow.AddDays(-1);

        // referenceTime = null → UtcNow kullanılır
        var result = sut.ComputeWeight(1.0, occurredAt, Guid.Empty);

        result.Should().BeGreaterThan(0);
        result.Should().BeLessThan(1.0);
    }

    [Fact]
    public void ComputeWeight_HighLambda_FasterDecay()
    {
        var fastDecayOptions = new TimeDecayOptions
        {
            Lambda = 0.1, // 10x daha hızlı decay
            SessionWindowMinutes = 30,
            ActiveSessionBoost = 0.5,
            RecentSessionBoost = 0.2
        };
        var sut = CreateService(fastDecayOptions);
        var now = DateTime.UtcNow;
        var occurredAt = now.AddDays(-10);

        var result = sut.ComputeWeight(1.0, occurredAt, Guid.Empty, now);

        // e^(-0.1 * 10) ≈ 0.368
        result.Should().BeApproximately(0.368, 0.01);
    }

    [Fact]
    public void ComputeWeight_BaseWeightMultiplied()
    {
        var sut = CreateService();
        var now = DateTime.UtcNow;
        
        var result1 = sut.ComputeWeight(1.0, now, Guid.Empty, now);
        var result3 = sut.ComputeWeight(3.0, now, Guid.Empty, now);

        result3.Should().BeApproximately(result1 * 3, 0.01);
    }
}
