using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for IdentityService's UserLoggedInEvent.
///
/// Kullanıcı giriş yaptığında öneri session'ını otomatik başlatır.
/// Böylece session tabanlı ağırlıklandırma login olur olmaz devreye girer.
/// </summary>
public class UserLoggedInAdapterConsumer : IConsumer<IdentityServiceUserLoggedInDto>
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<UserLoggedInAdapterConsumer> _logger;

    public UserLoggedInAdapterConsumer(
        ISessionService sessionService,
        ILogger<UserLoggedInAdapterConsumer> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<IdentityServiceUserLoggedInDto> context)
    {
        var dto = context.Message.UserId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<IdentityServiceUserLoggedInDto>(context);

        if (dto is null || dto.UserId == Guid.Empty)
        {
            _logger.LogError("[Adapter] UserLoggedIn could not be deserialized — UserId is empty.");
            return Task.CompletedTask;
        }

        var existingSession = _sessionService.GetActiveSession(dto.UserId);
        if (existingSession.HasValue)
        {
            _logger.LogInformation(
                "[Adapter] UserLoggedIn: UserId='{UserId}' already has active session '{SessionId}'. Reusing.",
                dto.UserId, existingSession.Value);
            return Task.CompletedTask;
        }

        var sessionId = _sessionService.StartSession(dto.UserId);

        _logger.LogInformation(
            "[Adapter] UserLoggedIn: Started recommendation session '{SessionId}' for UserId='{UserId}'.",
            sessionId, dto.UserId);

        return Task.CompletedTask;
    }
}
