$services = @("AadharService", "AuthService", "CentralPaymentGatewayService", "CompanyCrvService", "NotificationService", "TransactionsService", "UsersService")

$codeTemplate = @"
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace {REPLACE_NAMESPACE}.Integration;

public class ServiceDiscoveryHostedService : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly Config.Settings _settings;
    private readonly ILogger<ServiceDiscoveryHostedService> _logger;
    private bool _registered = false;

    public ServiceDiscoveryHostedService(HttpClient httpClient, Config.Settings settings, ILogger<ServiceDiscoveryHostedService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.ServiceDiscoveryEnabled)
        {
            return;
        }

        _logger.LogInformation("Starting Service Discovery for {ServiceName}", _settings.DiscoveryName);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        
        await HeartbeatAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await HeartbeatAsync(stoppingToken);
        }
    }

    private async Task HeartbeatAsync(CancellationToken stoppingToken)
    {
        try
        {
            var payload = new { name = _settings.DiscoveryName, url = _settings.InstanceUrl, ttl = 30 };
            
            if (!_registered)
            {
                var response = await _httpClient.PostAsJsonAsync($"{_settings.RegistryUrl}/register", payload, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    _registered = true;
                }
            }
            else
            {
                var response = await _httpClient.PostAsJsonAsync($"{_settings.RegistryUrl}/heartbeat", payload, stoppingToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _registered = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to communicate with service registry: {Message}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_settings.ServiceDiscoveryEnabled && _registered)
        {
            try
            {
                var payload = new { name = _settings.DiscoveryName, url = _settings.InstanceUrl, ttl = 30 };
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_settings.RegistryUrl}/register")
                {
                    Content = JsonContent.Create(payload)
                };
                await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception) { }
        }
        await base.StopAsync(cancellationToken);
    }
}
"@

foreach ($svc in $services) {
    $path = "d:\TGL\GDB\gdb-service-dotnet\$svc\Integration\ServiceDiscoveryHostedService.cs"
    if (Test-Path $path) {
        $content = $codeTemplate.Replace("{REPLACE_NAMESPACE}", $svc)
        Set-Content -Path $path -Value $content
        Write-Host "Updated $path"
    }
}

$accountsPath = "d:\TGL\GDB\gdb-service-dotnet\AccountsService\Integration\ServiceDiscovery.cs"
if (Test-Path $accountsPath) {
    $content = $codeTemplate.Replace("{REPLACE_NAMESPACE}", "AccountsService")
    Set-Content -Path $accountsPath -Value $content
    Write-Host "Updated $accountsPath"
}
