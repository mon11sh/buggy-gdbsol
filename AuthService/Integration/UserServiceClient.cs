using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthService.Config;
using AuthService.Domain.Exceptions;
using AuthService.Domain.Ports;
using Polly.CircuitBreaker;

namespace AuthService.Integration;

public class UserServiceClient : IUserServicePort
{
    private readonly HttpClient _httpClient;
    private readonly Settings _settings;
    private readonly ILogger<UserServiceClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Gdb.Common.Discovery.RegistryResolver? _registryResolver;

    public UserServiceClient(
        HttpClient httpClient, 
        Settings settings, 
        ILogger<UserServiceClient> logger, 
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _registryResolver = serviceProvider.GetService<Gdb.Common.Discovery.RegistryResolver>();
    }

    private async Task<string> ResolveAsync(CancellationToken ct = default)
    {
        if (_settings.ServiceDiscoveryEnabled && _registryResolver != null)
        {
            var url = await _registryResolver.ResolveAsync("users", _settings.UsersServiceUrl, ct);
            if (!string.IsNullOrEmpty(url)) return url.TrimEnd('/');
        }
        return _settings.UsersServiceUrl.TrimEnd('/');
    }

    public async Task<Dictionary<string, object>?> VerifyUserCredentialsAsync(string loginId, string password, CancellationToken ct = default)
    {
        var baseUri = await ResolveAsync(ct);
        var url = $"{baseUri}/internal/v1/users/verify";
        var payload = new { login_id = loginId, password = password };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        AddHeaders(request);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                _logger.LogInformation("Verified credentials for user {LoginId}", loginId);
                return data;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Invalid credentials for user {LoginId}", loginId);
                return null;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {LoginId} not found", loginId);
                return null;
            }

            _logger.LogError("User Service error: HTTP {StatusCode}", response.StatusCode);
            throw new ServiceUnavailableException("User service returned error");
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("User Service circuit breaker OPEN — failing fast");
            throw new ServiceUnavailableException("User service unavailable (circuit open)");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Failed to connect to User Service: {Message}", ex.Message);
            throw new ServiceUnavailableException("User service unavailable");
        }
    }

    public async Task<Dictionary<string, object>?> GetUserStatusAsync(string loginId, CancellationToken ct = default)
    {
        var baseUri = await ResolveAsync(ct);
        var url = $"{baseUri}/internal/v1/users/{loginId}/status";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                _logger.LogInformation("Retrieved status for user {LoginId}", loginId);
                return data;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {LoginId} not found", loginId);
                return null;
            }

            _logger.LogError("User Service error: HTTP {StatusCode}", response.StatusCode);
            throw new ServiceUnavailableException("User service returned error");
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("User Service circuit breaker OPEN — failing fast");
            throw new ServiceUnavailableException("User service unavailable (circuit open)");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Failed to connect to User Service: {Message}", ex.Message);
            throw new ServiceUnavailableException("User service unavailable");
        }
    }

    public async Task<string?> GetUserRoleAsync(string loginId, CancellationToken ct = default)
    {
        var baseUri = await ResolveAsync(ct);
        var url = $"{baseUri}/internal/v1/users/{loginId}/role";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("role", out var roleElement))
                {
                    var role = roleElement.GetString();
                    _logger.LogInformation("Retrieved role for user {LoginId}: {Role}", loginId, role);
                    return role;
                }
                return null;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {LoginId} not found", loginId);
                return null;
            }

            _logger.LogError("User Service error: HTTP {StatusCode}", response.StatusCode);
            throw new ServiceUnavailableException("User service returned error");
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("User Service circuit breaker OPEN — failing fast");
            throw new ServiceUnavailableException("User service unavailable (circuit open)");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Failed to connect to User Service: {Message}", ex.Message);
            throw new ServiceUnavailableException("User service unavailable");
        }
    }

    private void AddHeaders(HttpRequestMessage request)
    {
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
                request.Headers.Add("traceparent", GenerateTraceparent());
        }
        else
        {
            request.Headers.Add("traceparent", GenerateTraceparent());
        }
    }

    private string GenerateTraceparent()
    {
        var traceId = Guid.NewGuid().ToString("N");
        var spanId = Guid.NewGuid().ToString("N").Substring(0, 16);
        return $"00-{traceId}-{spanId}-01";
    }
}
