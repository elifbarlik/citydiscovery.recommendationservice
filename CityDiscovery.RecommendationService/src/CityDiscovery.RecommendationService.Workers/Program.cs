using CityDiscovery.RecommendationService.Infrastructure;
using CityDiscovery.RecommendationService.Workers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Register Infrastructure services (includes event handling)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
