namespace AadharService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "gdb-aadhar-service";
    public string AppVersion { get; set; } = "1.0.0";
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8005;

    public string ApiV1Prefix { get; set; } = "/api/v1";
    // Never defaulted in code: supplied via configuration/secret store (dev fallback is opt-in only).
    public string InternalApiKey { get; set; } = "";

    public bool ServiceDiscoveryEnabled { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";
    public string DiscoveryName { get; set; } = "aadhar";
    public string InstanceUrl { get; set; } = "http://localhost:8005";

    public string CorsAllowedOrigins { get; set; } = "http://localhost:3000;http://localhost:5173";
}
