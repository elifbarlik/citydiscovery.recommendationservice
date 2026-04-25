using CityDiscovery.RecommendationService.Application.Adapters.ExternalEvents;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CityDiscovery.RecommendationService.Application.Adapters.Consumers;

/// <summary>
/// Adapter consumer for IdentityService's UserDeletedEvent.
///
/// Kullanıcı silindiğinde RecommendationService'deki tüm kullanıcı verilerini temizler:
/// - InteractionLog: tüm etkileşim kayıtları
/// - UserPreference: kategori tercihleri
/// - UserProfile: embedding profili
/// - DismissedVenue: dismiss listesi
/// - Session: aktif oturum
/// </summary>
public class UserDeletedAdapterConsumer : IConsumer<IdentityServiceUserDeletedDto>
{
    private readonly IInteractionRepository _interactionRepository;
    private readonly IUserPreferenceRepository _userPreferenceRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IDismissedVenueRepository _dismissedVenueRepository;
    private readonly ISessionService _sessionService;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<UserDeletedAdapterConsumer> _logger;

    public UserDeletedAdapterConsumer(
        IInteractionRepository interactionRepository,
        IUserPreferenceRepository userPreferenceRepository,
        IUserProfileRepository userProfileRepository,
        IDismissedVenueRepository dismissedVenueRepository,
        ISessionService sessionService,
        IIdempotencyService idempotency,
        ILogger<UserDeletedAdapterConsumer> logger)
    {
        _interactionRepository = interactionRepository;
        _userPreferenceRepository = userPreferenceRepository;
        _userProfileRepository = userProfileRepository;
        _dismissedVenueRepository = dismissedVenueRepository;
        _sessionService = sessionService;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IdentityServiceUserDeletedDto> context)
    {
        var dto = context.Message.UserId != Guid.Empty
            ? context.Message
            : MassTransitEnvelopeHelper.Deserialize<IdentityServiceUserDeletedDto>(context);

        if (dto is null || dto.UserId == Guid.Empty)
        {
            _logger.LogError("[Adapter] UserDeleted could not be deserialized — UserId is empty.");
            return;
        }

        var eventId = GenerateDeterministicId(dto.UserId, dto.DeletedAtUtc);

        if (await _idempotency.ExistsAsync(eventId, context.CancellationToken))
        {
            _logger.LogInformation("[Adapter] UserDeleted '{EventId}' already processed. Skipping.", eventId);
            return;
        }

        _logger.LogInformation(
            "[Adapter] Processing UserDeleted: UserId='{UserId}', UserName='{UserName}'",
            dto.UserId, dto.UserName);

        await _interactionRepository.DeleteByUserIdAsync(dto.UserId, context.CancellationToken);
        await _userPreferenceRepository.DeleteByUserIdAsync(dto.UserId, context.CancellationToken);
        await _userProfileRepository.DeleteByUserIdAsync(dto.UserId, context.CancellationToken);
        await _dismissedVenueRepository.DeleteByUserIdAsync(dto.UserId, context.CancellationToken);
        _sessionService.EndSession(dto.UserId);

        await _idempotency.RecordAsync(eventId, "IdentityServiceUserDeletedEvent", context.CancellationToken);

        _logger.LogInformation(
            "[Adapter] Cleaned up all data for deleted user '{UserId}'.", dto.UserId);
    }

    private static Guid GenerateDeterministicId(Guid userId, DateTime timestamp)
    {
        var bytes = new byte[16];
        var userBytes = userId.ToByteArray();
        Array.Copy(userBytes, 0, bytes, 0, 8);
        var tickBytes = BitConverter.GetBytes(timestamp.Ticks);
        Array.Copy(tickBytes, 0, bytes, 8, 8);
        return new Guid(bytes);
    }
}
