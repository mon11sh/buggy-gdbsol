namespace AccountsService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "GDB-Accounts-Service";
    public string Title { get; set; } = "GDB Accounts Service";
    public string Description { get; set; } = "Microservice for managing bank accounts";
    public string AppVersion { get; set; } = "1.0.0";
    
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8001;
    
    public bool ServiceDiscoveryEnabled { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";
    public string DiscoveryName { get; set; } = "accounts";
    public string InstanceUrl { get; set; } = "http://localhost:8001";
    
    public string AllowedHosts { get; set; } = "localhost,127.0.0.1,accounts-service,*.gdb.local";
    
    // Data-access strategy: "EfCore" (default) or "AdoNet". Also honoured via the DATA_ACCESS
    // environment variable (applied in Program.cs). AdoNet is supported only for the sqlserver provider.
    public string DataAccess { get; set; } = "EfCore";

    public string DatabaseProvider { get; set; } = "postgres";
    public bool AutoCreateTables { get; set; } = true;
    public string DatabaseUrl { get; set; } = "postgresql://user:password@localhost:5432/gdb_accounts_db";
    public string DatabaseHost { get; set; } = "localhost";
    public int DatabasePort { get; set; } = 5432;
    public string DatabaseName { get; set; } = "gdb_accounts_db";
    public string DatabaseUser { get; set; } = "postgres";
    public string DatabasePassword { get; set; } = "";
    
    public string PinEncryptionKey { get; set; } = "";
    // Re-encrypt/re-index Aadhaar rows still in the pre-upgrade format at startup (idempotent, cheap when done).
    public bool AadhaarCryptoUpgradeOnStartup { get; set; } = true;
    public string JwtSecretKey { get; set; } = "";
    public string JwtPublicKey { get; set; } = "";
    public string JwtAlgorithm { get; set; } = "HS256";
    // Tokens are accepted only when minted by this issuer for this audience.
    public string JwtIssuer { get; set; } = Gdb.Common.Security.JwtValidationExtensions.DefaultIssuer;
    public string JwtAudience { get; set; } = Gdb.Common.Security.JwtValidationExtensions.DefaultAudience;
    // Production only: apply versioned EF migrations at startup (set false when a CI/CD job owns schema changes).
    public bool MigrateOnStartup { get; set; } = true;
    public int AccessTokenExpireMinutes { get; set; } = 30;
    public bool DisableAuth { get; set; } = false;
    // Fail-closed by default: with false, empty/default secrets refuse to start (see SecureConfigGuard).
    // Set true ONLY for local development / the teaching stack.
    public bool AllowInsecureDefaults { get; set; } = false;
    public string InternalApiKey { get; set; } = "";
    
    public string AccountsServiceUrl { get; set; } = "http://localhost:8001";
    public string TransactionsServiceUrl { get; set; } = "http://localhost:8002";
    public string UsersServiceUrl { get; set; } = "http://localhost:8003";
    public string AuthServiceUrl { get; set; } = "http://localhost:8004";
    public string AadharServiceUrl { get; set; } = "http://localhost:8005";
    public string CompanyServiceUrl { get; set; } = "http://localhost:8006";
    public string NotificationServiceUrl { get; set; } = "http://localhost:8007";
    public string PaymentGatewayServiceUrl { get; set; } = "http://localhost:8008";
    
    public int AccountServiceTimeout { get; set; } = 10;
    public string ApiPrefix { get; set; } = "/api/v1";
    public string CorsAllowedOrigins { get; set; } = "http://localhost:3000,http://localhost:5173,http://localhost:8001,http://localhost:8002,http://localhost:8003,http://localhost:8004";
}
