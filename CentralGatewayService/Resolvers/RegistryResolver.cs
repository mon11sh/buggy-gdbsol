using System.Collections.Concurrent;
using System.Text.Json;
using CentralGatewayService.Config;

namespace CentralGatewayService.Resolvers;

/// <summary>
/// Resolves a backend through the RegistryService, falling back to static configuration. Positive
/// answers are cached briefly so the gateway does not add a registry round trip to EVERY proxied
/// request (the registry is only consulted again after <see cref="CacheTtl"/>).
/// </summary>
public class RegistryResolver : IServiceResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    private readonly string _registryUrl;
    private readonly IServiceResolver _fallbackResolver;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RegistryResolver> _logger;
    private readonly ConcurrentDictionary<string, (string Url, DateTime ExpiresUtc)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RegistryResolver(Settings settings, IServiceResolver fallbackResolver, IHttpClientFactory httpClientFactory, ILogger<RegistryResolver> logger)
    {
        _registryUrl = settings.RegistryUrl.TrimEnd('/');
        _fallbackResolver = fallbackResolver;
        _httpClient = httpClientFactory.CreateClient("RegistryClient");
        _httpClient.Timeout = TimeSpan.FromSeconds(2);
        _logger = logger;
    }

    public async Task<string?> ResolveAsync(string serviceName)
    {
        if (_cache.TryGetValue(serviceName, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            return cached.Url;

        try
        {
            var response = await _httpClient.GetAsync($"{_registryUrl}/api/v1/registry/resolve/{serviceName}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("url", out var urlElement) && urlElement.GetString() is { Length: > 0 } url)
                {
                    _cache[serviceName] = (url, DateTime.UtcNow + CacheTtl);
                    return url;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to resolve service '{ServiceName}' from registry: {Message}. Falling back to static configuration.", serviceName, ex.Message);
        }

        // Fallback to static resolution (not cached: the registry may come back).
        return await _fallbackResolver.ResolveAsync(serviceName);
    }
}
