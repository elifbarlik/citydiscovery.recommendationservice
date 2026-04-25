namespace CityDiscovery.RecommendationService.Application.Options;

public class TimeDecayOptions
{
    public const string SectionName = "TimeDecay";

    /// <summary>
    /// Lambda (λ) for exponential decay. Default 0.01 ≈ half-life 70 days.
    /// weight = base × e^(-λ × days)
    /// </summary>
    public double Lambda { get; set; } = 0.01;

    /// <summary>
    /// Session window in minutes. Interactions within this window get ActiveSessionBoost.
    /// </summary>
    public int SessionWindowMinutes { get; set; } = 30;

    /// <summary>
    /// Boost added when interaction is within SessionWindowMinutes of reference time.
    /// </summary>
    public double ActiveSessionBoost { get; set; } = 0.5;

    /// <summary>
    /// Boost added when interaction has same SessionId but is older than SessionWindowMinutes.
    /// </summary>
    public double RecentSessionBoost { get; set; } = 0.2;
}
