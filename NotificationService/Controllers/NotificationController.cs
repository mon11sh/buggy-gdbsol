using Gdb.Common.Security;
using Microsoft.AspNetCore.Mvc;
using NotificationService.DTOs;
using NotificationService.Services;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/v1/notify")]
[InternalApi]
public class NotificationController : ControllerBase
{
    private readonly NotificationAppService _service;
    private readonly NotificationStorageService _storage;

    public NotificationController(NotificationAppService service, NotificationStorageService storage)
    {
        _service = service;
        _storage = storage;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        await _service.SendNotificationAsync(request);
        return Ok(new { status = "sent", type = request.Type.ToString() });
    }

    [HttpGet("{identifier}")]
    public async Task<IActionResult> GetNotifications(string identifier)
    {
        var notifications = await _storage.GetNotificationsAsync(identifier);
        return Ok(notifications);
    }

    [HttpPost("read/{identifier}")]
    public async Task<IActionResult> MarkAsRead(string identifier)
    {
        await _storage.MarkAllAsReadAsync(identifier);
        return Ok(new { status = "success", message = $"Notifications for {identifier} marked as read" });
    }

    [HttpDelete("{identifier}")]
    public async Task<IActionResult> ClearNotifications(string identifier, [FromHeader(Name = "X-Internal-API-Key")] string? xInternalApiKey = null)
    {
        await _storage.ClearNotificationsAsync(identifier);
        return Ok(new { status = "success", message = $"Notifications for {identifier} cleared" });
    }
}

