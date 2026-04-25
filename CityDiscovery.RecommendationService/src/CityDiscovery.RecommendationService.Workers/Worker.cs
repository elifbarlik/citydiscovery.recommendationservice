namespace CityDiscovery.RecommendationService.Workers;

/// <summary>
/// Minimal background service used as a liveness probe.
/// The actual message consumption is handled by MassTransit's hosted service
/// (registered via <c>AddMassTransit</c> in Infrastructure DI).
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecommendationService Worker started at: {Time}", DateTimeOffset.UtcNow);
        _logger.LogInformation("MassTransit bus is consuming events from RabbitMQ.");

        // Keep alive — MassTransit hosted service drives actual message consumption.
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("RecommendationService Worker stopping.");
    }
}

