using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using TransactionsService.Config;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Integration.Contracts;

namespace TransactionsService.Integration;

public class AccountServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly Settings _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public AccountServiceClient(HttpClient httpClient, Settings settings, IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Internal request with a JSON body. PINs and amounts must never ride in the query string
    /// (they land in proxy/access logs); AccountsService binds these as snake_case DTOs.
    /// </summary>
    private HttpRequestMessage CreateInternalJsonRequest(HttpMethod method, string url, object body)
    {
        var request = CreateInternalRequest(method, url);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }),
            System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    private HttpRequestMessage CreateInternalRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
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
        
        return request;
    }

    private async Task<string> ResolveAsync(CancellationToken ct = default)
    {
        string baseUrl = _settings.AccountsServiceUrl;
        if (_settings.ServiceDiscoveryEnabled)
        {
            var resolver = _serviceProvider.GetService<Gdb.Common.Discovery.RegistryResolver>();
            if (resolver != null)
            {
                baseUrl = await resolver.ResolveAsync("accounts", _settings.AccountsServiceUrl, ct) ?? baseUrl;
            }
        }
        return baseUrl.TrimEnd('/');
    }

    public async Task<InternalAccountDto> ValidateAccountAsync(int accountNumber, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = await ResolveAsync(ct);
            var url = $"{baseUrl}/api/v1/internal/accounts/{accountNumber}";
            var request = CreateInternalRequest(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidAccountException(accountNumber, $"Account {accountNumber} not found");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<InternalAccountDto>(content) ?? new InternalAccountDto();
                if (!data.IsActive)
                    throw new SourceAccountInactiveException(accountNumber, $"Account {accountNumber} is not active");
                return data;
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            throw new ServiceUnavailableException("AccountsService", $"Account Service error: {error}");
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service temporarily unavailable (circuit open)");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service is currently unavailable");
        }
    }

    public async Task<bool> VerifyPinAsync(int accountNumber, string pin, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = await ResolveAsync(ct);
            var url = $"{baseUrl}/api/v1/internal/accounts/{accountNumber}/verify-pin";
            var request = CreateInternalJsonRequest(HttpMethod.Post, url, new { pin });
            
            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidAccountException(accountNumber, $"Account {accountNumber} not found");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new InvalidPinException("Invalid PIN provided");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                if (!data.TryGetProperty("pin_valid", out var pinValid) || !pinValid.GetBoolean())
                    throw new InvalidPinException("Invalid PIN provided");
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            throw new ServiceUnavailableException("AccountsService", $"PIN verification failed: {error}");
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service temporarily unavailable (circuit open)");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service is unavailable");
        }
    }

    public async Task<InternalBalanceResult> DebitAccountAsync(int accountNumber, decimal amount, string description = "Transaction", CancellationToken ct = default)
    {
        try
        {
            var baseUrl = await ResolveAsync(ct);
            var url = $"{baseUrl}/api/v1/internal/accounts/{accountNumber}/debit";
            var request = CreateInternalJsonRequest(HttpMethod.Post, url, new { amount, description });
            
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<InternalBalanceResult>(content) ?? new InternalBalanceResult();
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                var (errorCode, errorMessage) = ExtractError(errorContent);

                // A transient optimistic-concurrency conflict is NOT an insufficient-funds condition:
                // surface it as a retryable service failure instead of telling the customer they are broke.
                if (string.Equals(errorCode, "CONCURRENCY_CONFLICT", StringComparison.OrdinalIgnoreCase))
                    throw new ServiceUnavailableException("AccountsService", "Account is busy, please retry the debit");

                throw new InsufficientFundsException(accountNumber, errorMessage ?? errorContent);
            }

            throw new ServiceUnavailableException("AccountsService", "Debit operation failed");
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service temporarily unavailable (circuit open)");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service is unavailable");
        }
    }

    /// <summary>
    /// Pulls (error_code, message) out of an AccountsService error body without ever throwing —
    /// the previous inline parse swallowed its own exception and made the real message unreachable.
    /// </summary>
    private static (string? Code, string? Message) ExtractError(string body)
    {
        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(body);
            if (data.ValueKind != JsonValueKind.Object) return (null, null);

            string? code = data.TryGetProperty("error_code", out var c) ? c.GetString() : null;
            string? message =
                data.TryGetProperty("error_message", out var m1) ? m1.GetString() :
                data.TryGetProperty("message", out var m2) ? m2.GetString() :
                data.TryGetProperty("error", out var m3) ? m3.GetString() : null;
            return (code, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    public async Task<InternalBalanceResult> CreditAccountAsync(int accountNumber, decimal amount, string description = "Transaction", CancellationToken ct = default)
    {
        try
        {
            var baseUrl = await ResolveAsync(ct);
            var url = $"{baseUrl}/api/v1/internal/accounts/{accountNumber}/credit";
            var request = CreateInternalJsonRequest(HttpMethod.Post, url, new { amount, description });
            
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<InternalBalanceResult>(content) ?? new InternalBalanceResult();
            }

            throw new ServiceUnavailableException("AccountsService", "Credit operation failed");
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service temporarily unavailable (circuit open)");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service is unavailable");
        }
    }

    public async Task<string> GetAccountPrivilegeAsync(int accountNumber, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = await ResolveAsync(ct);
            var url = $"{baseUrl}/api/v1/internal/accounts/{accountNumber}/privilege";
            var request = CreateInternalRequest(HttpMethod.Get, url);
            
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                if (data.TryGetProperty("privilege", out var privilege))
                    return privilege.GetString() ?? "SILVER";
            }

            throw new ServiceUnavailableException("AccountsService", "Could not fetch account privilege");
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service temporarily unavailable (circuit open)");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new ServiceUnavailableException("AccountsService", "Account Service is unavailable");
        }
    }
}
