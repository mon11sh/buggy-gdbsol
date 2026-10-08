using NotificationService.DTOs;

namespace NotificationService.Services;

public class NotificationAppService
{
    private readonly ILogger<NotificationAppService> _logger;
    private readonly NotificationStorageService _storage;

    public NotificationAppService(ILogger<NotificationAppService> logger, NotificationStorageService storage)
    {
        _logger = logger;
        _storage = storage;
    }

    public async Task<bool> SendNotificationAsync(NotificationRequest request)
    {
        _logger.LogInformation("[NOTIFICATION] To: {Recipient} | Type: {Type} | Msg: {Message}", request.Recipient, request.Type, request.Message);

        await _storage.AddNotificationAsync(
            recipient: request.Recipient,
            message: request.Message,
            type: request.Type.ToString(),
            mode: request.Mode
        );

        return true;
    }
}
