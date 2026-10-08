using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using TransactionsService.Config;

namespace TransactionsService.Integration;

public class NotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly Settings _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationClient> _logger;

    public NotificationClient(HttpClient httpClient, Settings settings, IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider, ILogger<NotificationClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task SendNotificationAsync(int accountId, string type, string message, CancellationToken ct = default)
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

            var url = $"{baseUrl.TrimEnd('/')}/api/v1/notify/send";
            var payload = new
            {
                recipient = accountId.ToString(),
                message = message,
                type = type,
                mode = (string?)null
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
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
            }

            await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort by contract (a lost notification never fails a money operation) - but never silent.
            _logger.LogWarning("Notification {Type} for account {AccountId} was not delivered: {Message}", type, accountId, ex.Message);
        }
    }
}
