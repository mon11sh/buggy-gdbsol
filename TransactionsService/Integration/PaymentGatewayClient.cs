using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using TransactionsService.Config;

namespace TransactionsService.Integration;

public class PaymentGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly Settings _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public PaymentGatewayClient(HttpClient httpClient, Settings settings, IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ValidatePaymentAsync(int sourceAccount, int destAccount, decimal amount, string mode, CancellationToken ct = default)
    {
        try
        {
            string baseUrl = _settings.PaymentGatewayServiceUrl;
            if (_settings.ServiceDiscoveryEnabled)
            {
                var resolver = _serviceProvider.GetService<Gdb.Common.Discovery.RegistryResolver>();
                if (resolver != null)
                {
                    baseUrl = await resolver.ResolveAsync("payment", _settings.PaymentGatewayServiceUrl, ct) ?? baseUrl;
                }
            }

            var url = $"{baseUrl.TrimEnd('/')}/api/v1/payment/process";
            var payload = new
            {
                source_account_id = sourceAccount,
                destination_account_id = destAccount,
                amount = amount,
                mode = mode,
                reference_id = (string?)null
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

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(responseContent);
                if (data.TryGetProperty("success", out var success))
                    return success.GetBoolean();
                return false;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
