using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Gdb.Common.Discovery;

public sealed record ServiceDiscoveryOptions(string Name, string InstanceUrl, string RegistryUrl);

public static class ServiceDiscoveryExtensions
{
    /// <summary>Registers this instance with the registry and keeps it alive with heartbeats.</summary>
    public static IServiceCollection AddGdbServiceDiscovery(this IServiceCollection services, string name, string instanceUrl, string registryUrl)
    {
        services.AddHttpClient();
        services.AddSingleton(new ServiceDiscoveryOptions(name, instanceUrl, registryUrl));
        services.AddHostedService<ServiceDiscoveryHostedService>();
        return services;
    }
}

/// <summary>
/// Register-then-heartbeat loop against the RegistryService (one copy for all services; it used
/// to be duplicated verbatim in eight projects). Deregisters on graceful shutdown.
/// </summary>
public sealed class ServiceDiscoveryHostedService : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private const int TtlSeconds = 30;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceDiscoveryOptions _options;
    private readonly ILogger<ServiceDiscoveryHostedService> _logger;
    private bool _registered;

    public ServiceDiscoveryHostedService(IHttpClientFactory httpClientFactory, ServiceDiscoveryOptions options, ILogger<ServiceDiscoveryHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    private object Payload => new { name = _options.Name, url = _options.InstanceUrl, ttl = TtlSeconds };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting service discovery for {ServiceName} -> {RegistryUrl}", _options.Name, _options.RegistryUrl);

        using var timer = new PeriodicTimer(HeartbeatInterval);
        await HeartbeatAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await HeartbeatAsync(stoppingToken);
    }

    private async Task HeartbeatAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            if (!_registered)
            {
                var response = await client.PostAsJsonAsync($"{_options.RegistryUrl}/register", Payload, ct);
                _registered = response.IsSuccessStatusCode;
            }
            else
            {
                var response = await client.PostAsJsonAsync($"{_options.RegistryUrl}/heartbeat", Payload, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    _registered = false; // registry restarted: re-register on the next tick
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Service registry unreachable: {Message}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registered)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_options.RegistryUrl}/register") { Content = JsonContent.Create(Payload) };
                await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Deregistration skipped: {Message}", ex.Message); // TTL expiry cleans it up
            }
        }
        await base.StopAsync(cancellationToken);
    }
}
