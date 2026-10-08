namespace CentralGatewayService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "gdb-central-gateway-service";
    public string AppVersion { get; set; } = "1.0.0";
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8000;

    public bool ServiceDiscoveryEnabled { get; set; } = false;

    /// <summary>Trust X-Forwarded-For/Proto from the ingress in front of the gateway (rate limits then key on the real client IP). Only enable behind a proxy you control.</summary>
    public bool TrustForwardedHeaders { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";

    public int RateLimitPerMin { get; set; } = 0;
    public int RateBurst { get; set; } = 0;

    public string CorsAllowedOrigins { get; set; } = "http://localhost:3000;http://localhost:5173;http://localhost:8000";

    public Dictionary<string, string> GatewayBackends { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
