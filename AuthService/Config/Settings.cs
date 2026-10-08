namespace AuthService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "gdb-auth-service";
    public string AppVersion { get; set; } = "1.0.0";
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8004;

    // Data-access strategy: "EfCore" (default) or "AdoNet". Also honoured via the DATA_ACCESS
    // environment variable (applied in Program.cs). AdoNet is supported only for the sqlserver provider.
    public string DataAccess { get; set; } = "EfCore";

    public string DatabaseUrl { get; set; } = "";
    public string DatabaseProvider { get; set; } = "sqlite";
    public string DatabaseHost { get; set; } = "localhost";
    public int DatabasePort { get; set; } = 5432;
    public string DatabaseUser { get; set; } = "postgres";
    public string DatabasePassword { get; set; } = "password";
    public string DatabaseName { get; set; } = "gdb_auth";

    public string JwtSecretKey { get; set; } = "";
    // Fail-closed by default: with false, empty/default secrets refuse to start (see SecureConfigGuard).
    // Set true ONLY for local development / the teaching stack.
    public bool AllowInsecureDefaults { get; set; } = false;
    public string JwtAlgorithm { get; set; } = "HS256";
    // Every minted token carries these; resource services validate against the same values.
    public string JwtIssuer { get; set; } = Gdb.Common.Security.JwtValidationExtensions.DefaultIssuer;
    public string JwtAudience { get; set; } = Gdb.Common.Security.JwtValidationExtensions.DefaultAudience;
    // Production only: apply versioned EF migrations at startup (set false when a CI/CD job owns schema changes).
    public bool MigrateOnStartup { get; set; } = true;
    public int JwtExpirationMinutes { get; set; } = 30;
    // Refresh tokens: long-lived, rotated on every use, delivered in an httpOnly cookie (or the body for non-browser clients).
    public int RefreshTokenDays { get; set; } = 7;
    public string RefreshCookieName { get; set; } = "gdb_refresh";
    // "/" so the cookie also travels through the gateway prefix (/auth/...); SameSite=Strict + HttpOnly keep it contained.
    public string RefreshCookiePath { get; set; } = "/";
    public string JwtPrivateKey { get; set; } = "";
    public string JwtPublicKey { get; set; } = "";

    public string InternalApiKey { get; set; } = "";

    public bool ServiceDiscoveryEnabled { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";
    public string DiscoveryName { get; set; } = "auth";
    public string InstanceUrl { get; set; } = "http://localhost:8004";

    public string UsersServiceUrl { get; set; } = "http://localhost:8003";
    public int UserServiceTimeout { get; set; } = 10;

    public string CorsAllowedOrigins { get; set; } = "http://localhost:3000;http://localhost:5173";
    public bool AutoCreateTables { get; set; } = true;
}
