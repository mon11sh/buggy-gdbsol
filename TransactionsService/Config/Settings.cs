namespace TransactionsService.Config;

public class Settings
{
    public string Environment { get; set; } = "development";
    public string AppName { get; set; } = "GDB-Transaction-Service";
    public string Title { get; set; } = "GDB Transaction Service";
    public string Description { get; set; } = "Microservice for handling withdrawals, deposits, transfers, and transaction logging";
    public string AppVersion { get; set; } = "1.0.0";

    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8002;

    public bool ServiceDiscoveryEnabled { get; set; } = false;
    public string RegistryUrl { get; set; } = "http://localhost:8010";
    public string DiscoveryName { get; set; } = "transactions";
    public string InstanceUrl { get; set; } = "http://localhost:8002";

    // Data-access strategy: "EfCore" (default) or "AdoNet". Also honoured via the DATA_ACCESS
    // environment variable (applied in Program.cs). AdoNet is supported only for the sqlserver provider.
    public string DataAccess { get; set; } = "EfCore";

    // Reconciliation worker (see Services/TransferReconciliationService).
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    // A transfer still PENDING after this long is considered stuck (the process died mid-saga).
    public int StaleTransferMinutes { get; set; } = 10;
    // An idempotency reservation (202) older than this is an orphan and is released.
    public int IdempotencyReservationTtlMinutes { get; set; } = 15;

    public string DatabaseProvider { get; set; } = "postgres";
    public bool AutoCreateTables { get; set; } = true;
    public string DatabaseUrl { get; set; } = "";
    
    public string DatabaseHost { get; set; } = "localhost";
    public int DatabasePort { get; set; } = 5432;
    public string DatabaseName { get; set; } = "gdb_transactions_db";
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

    public string AccountsServiceUrl { get; set; } = "http://localhost:8001";
    public string NotificationServiceUrl { get; set; } = "http://localhost:8007";
    public string PaymentGatewayServiceUrl { get; set; } = "http://localhost:8008";

    public int AccountServiceTimeout { get; set; } = 10;
    public string ApiPrefix { get; set; } = "/api/v1";
    public string CorsAllowedOrigins { get; set; } = "http://localhost:3000;http://localhost:5173";

    public decimal MinimumDepositAmount { get; set; } = 1.00m;
    public decimal MinimumWithdrawalAmount { get; set; } = 1.00m;
    public decimal MinimumTransferAmount { get; set; } = 1.00m;
    public decimal MaximumTransactionAmount { get; set; } = 999999999.99m;
    public string LOG_DIR { get; set; } = "logs";
}
