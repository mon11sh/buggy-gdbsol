namespace UsersService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string ServiceName { get; set; } = "gdb-users-service";
    public string Title { get; set; } = "GDB User Management Service";
    public string Description { get; set; } = "Microservice for managing users, roles, and authentication records";
    public string ServiceVersion { get; set; } = "1.0.0";

    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8003;

    public bool ServiceDiscoveryEnabled { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";
    public string DiscoveryName { get; set; } = "users";
    public string InstanceUrl { get; set; } = "http://localhost:8003";

    // Data-access strategy: "EfCore" (default) or "AdoNet". Also honoured via the DATA_ACCESS
    // environment variable (applied in Program.cs). AdoNet is supported only for the sqlserver provider.
    public string DataAccess { get; set; } = "EfCore";

    public string DatabaseProvider { get; set; } = "sqlite";
    public bool AutoCreateTables { get; set; } = true;
    public string DatabaseUrl { get; set; } = "";
    
    public string DatabaseHost { get; set; } = "localhost";
    public int DatabasePort { get; set; } = 5432;
    public string DatabaseName { get; set; } = "gdb_users_db";
    public string DatabaseUser { get; set; } = "postgres";
    public string DatabasePassword { get; set; } = "";

    public string JwtSecretKey { get; set; } = "";
    // Fail-closed by default: empty/default secrets refuse to start unless this is true (dev only).
    public bool AllowInsecureDefaults { get; set; } = false;
    public string JwtPublicKey { get; set; } = "";
    public string JwtAlgorithm { get; set; } = "HS256";
    // Tokens are accepted only when minted by this issuer for this audience.
    public string JwtIssuer { get; set; } = Gdb.Common.Security.JwtValidationExtensions.DefaultIssuer;
    public string JwtAudience { get; set; } = Gdb.Common.Security.JwtValidationExtensions.DefaultAudience;
    // Production only: apply versioned EF migrations at startup (set false when a CI/CD job owns schema changes).
    public bool MigrateOnStartup { get; set; } = true;
    public string InternalApiKey { get; set; } = "";

    public string ApiPrefix { get; set; } = "/api/v1";
    public string CorsOrigins { get; set; } = "http://localhost:3000;http://localhost:5173";
    
    public string AuthServiceUrl { get; set; } = "http://localhost:8004";
}
