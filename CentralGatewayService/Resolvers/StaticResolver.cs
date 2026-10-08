using CentralGatewayService.Config;

namespace CentralGatewayService.Resolvers;

public class StaticResolver : IServiceResolver
{
    private readonly Dictionary<string, string> _backends;

    public StaticResolver(Settings settings, IConfiguration configuration)
    {
        _backends = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Load defaults from Settings
        foreach (var kvp in settings.GatewayBackends)
        {
            _backends[kvp.Key] = kvp.Value;
        }

        // Apply environment overrides GATEWAY_{NAME}_URL
        foreach (var key in _backends.Keys.ToList())
        {
            var envOverride = Environment.GetEnvironmentVariable($"GATEWAY_{key.ToUpper()}_URL") 
                              ?? configuration[$"GATEWAY_{key.ToUpper()}_URL"];
            if (!string.IsNullOrEmpty(envOverride))
            {
                _backends[key] = envOverride.TrimEnd('/');
            }
        }
    }

    public Task<string?> ResolveAsync(string serviceName)
    {
        if (_backends.TryGetValue(serviceName, out var url))
        {
            return Task.FromResult<string?>(url);
        }
        return Task.FromResult<string?>(null);
    }
}
