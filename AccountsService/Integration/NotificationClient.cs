using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using AccountsService.Config;

namespace AccountsService.Integration;

public interface INotificationClient
{
    Task SendNotificationAsync(string userId, string message, string notificationType = "INFO", string? mode = null, CancellationToken ct = default);
}

public class NotificationClient : INotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly Settings _settings;
    private readonly ILogger<NotificationClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public NotificationClient(HttpClient httpClient, Settings settings, ILogger<NotificationClient> logger, IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    public async Task SendNotificationAsync(string userId, string message, string notificationType = "INFO", string? mode = null, CancellationToken ct = default)
    {
        try
        {
            string baseUrl = _settings.NotificationServiceUrl;
            if (_settings.ServiceDiscoveryEnabled)
            {
                var resolver = _serviceProvider.GetService<Gdb.Common.Discovery.RegistryResolver>();
                if (resolver != null)
                {
                    baseUrl = await resolver.ResolveAsync("notification", _settings.NotificationServiceUrl, ct) ?? baseUrl;
                }
            }

            var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/notify/send";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = JsonContent.Create(new
                {
                    recipient = userId,
                    message = message,
                    type = notificationType,
                    mode = mode
                })
            };

            request.Headers.Add("X-Internal-API-Key", _settings.InternalApiKey);
            
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                var correlationId = context.Items["CorrelationId"]?.ToString();
                if (!string.IsNullOrEmpty(correlationId))
                    request.Headers.Add("X-Correlation-ID", correlationId);

                var traceId = context.Items["TraceId"]?.ToString();
                if (!string.IsNullOrEmpty(traceId))
                    request.Headers.Add("traceparent", $"00-{traceId}-{Guid.NewGuid().ToString("N").Substring(0, 16)}-01");
                else
                    request.Headers.Add("traceparent", Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToHexString());
            }
            else
            {
                request.Headers.Add("traceparent", Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToHexString());
            }

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Notification sent to {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Failed to send notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Notification service error: {Message}", ex.Message);
            // Don't fail the main process if notification fails
        }
    }
}
