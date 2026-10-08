using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Text.Json;
using AccountsService.Config;

namespace AccountsService.Integration;

public interface ICompanyClient
{
    Task<Dictionary<string, object>> VerifyRegistrationAsync(string registrationNumber, CancellationToken ct = default);
}

public class CompanyClient : ICompanyClient
{
    private readonly HttpClient _httpClient;
    private readonly Settings _settings;
    private readonly ILogger<CompanyClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public CompanyClient(HttpClient httpClient, Settings settings, ILogger<CompanyClient> logger, IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    public async Task<Dictionary<string, object>> VerifyRegistrationAsync(string registrationNumber, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling Company CRV service for verification: {RegMasked}*************", registrationNumber.Length > 8 ? registrationNumber.Substring(0, 8) : registrationNumber);

        string baseUrl = _settings.CompanyServiceUrl;
        if (_settings.ServiceDiscoveryEnabled)
        {
            var resolver = _serviceProvider.GetService<Gdb.Common.Discovery.RegistryResolver>();
            if (resolver != null)
            {
                baseUrl = await resolver.ResolveAsync("company", _settings.CompanyServiceUrl, ct) ?? baseUrl;
            }
        }

        var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/company/verify";
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = JsonContent.Create(new { registration_number = registrationNumber })
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

        try
        {
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content) 
                    ?? new Dictionary<string, object>();
                
                _logger.LogInformation("Company verification response: {Status}", result.GetValueOrDefault("status"));
                return result;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var errorDetail = "Invalid registration number format";
                try 
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                    if (result?.ContainsKey("detail") == true)
                        errorDetail = result["detail"].ToString() ?? errorDetail;
                }
                catch (JsonException) { /* error body was not JSON; keep the default message */ }
                
                _logger.LogError("Company validation error: {ErrorDetail}", errorDetail);
                throw new ArgumentException($"Company validation error: {errorDetail}");
            }
            else
            {
                _logger.LogError("Company CRV service returned status {StatusCode}", response.StatusCode);
                throw new Exception($"Company CRV service error: HTTP {(int)response.StatusCode}");
            }
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            _logger.LogWarning("Company CRV service circuit breaker OPEN — failing fast");
            throw new Exception("Company verification service is unavailable (circuit open)");
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Company CRV service request timed out");
            throw new Exception("Company verification service is unavailable (timeout)");
        }
        catch (HttpRequestException e)
        {
            _logger.LogError("HTTP error calling Company CRV service: {Message}", e.Message);
            throw new Exception($"Company verification service error: {e.Message}");
        }
    }
}
