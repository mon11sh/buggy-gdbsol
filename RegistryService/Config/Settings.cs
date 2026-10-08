namespace RegistryService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "gdb-registry-service";
    public string AppVersion { get; set; } = "1.0.0";
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8010;
}
