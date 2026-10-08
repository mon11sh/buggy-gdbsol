namespace CentralPaymentGatewayService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "gdb-central-payment-gateway";
    public string AppVersion { get; set; } = "1.0.0";
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8008;

    public bool ServiceDiscoveryEnabled { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";
    public string DiscoveryName { get; set; } = "payment_gateway";
    public string InstanceUrl { get; set; } = "http://localhost:8008";

    // Never defaulted in code: supplied via configuration/secret store (dev fallback is opt-in only).
    public string InternalApiKey { get; set; } = "";
    public string CorsAllowedOrigins { get; set; } = "http://localhost:3000;http://localhost:5173";
}
