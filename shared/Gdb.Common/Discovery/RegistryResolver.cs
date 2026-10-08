using System.Text.Json;

namespace Gdb.Common.Discovery;

public class RegistryResolver
{
    private readonly string _registryUrl;
    private readonly Dictionary<string, string> _fallback;
    private readonly double _cacheTtlSeconds;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, (string Url, long ExpiresAt)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    protected virtual long GetNow() => Environment.TickCount64;

    public RegistryResolver(
        IHttpClientFactory httpClientFactory,
        string registryUrl,
        IDictionary<string, string>? fallback = null,
        double cacheTtlSeconds = 10.0,
        double requestTimeoutSeconds = 2.0)
    {
        _registryUrl = registryUrl.TrimEnd('/');
        _fallback = new Dictionary<string, string>(fallback ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _cacheTtlSeconds = cacheTtlSeconds;
        
        _httpClient = httpClientFactory.CreateClient("RegistryResolverClient");
        _httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    }

    private async Task<string?> QueryAsync(string name, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_registryUrl}/resolve/{name}", ct);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var json = JsonSerializer.Deserialize<JsonElement>(content);
                if (json.TryGetProperty("url", out var urlElement))
                {
                    return urlElement.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            // Fall back gracefully
            Console.WriteLine($"Registry resolve('{name}') failed: {ex.Message}");
        }
        return null;
    }

    public async Task<string?> ResolveAsync(string name, string? fallbackUrl = null, CancellationToken ct = default)
    {
        var now = GetNow();
        
        lock (_lock)
        {
            if (_cache.TryGetValue(name, out var cached) && cached.ExpiresAt > now)
            {
                return cached.Url;
            }
        }

        var url = await QueryAsync(name, ct);
        
        if (!string.IsNullOrEmpty(url))
        {
            lock (_lock)
            {
                // Environment.TickCount64 is ms, cache ttl is seconds
                _cache[name] = (url, now + (long)(_cacheTtlSeconds * 1000));
            }
            return url;
        }

        if (!string.IsNullOrEmpty(fallbackUrl))
        {
            return fallbackUrl;
        }
        
        if (_fallback.TryGetValue(name, out var mappedUrl))
        {
            return mappedUrl;
        }

        return null;
    }
}
