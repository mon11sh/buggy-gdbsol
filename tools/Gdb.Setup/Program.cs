using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gdb.Setup
{
    class Program
    {
        static readonly string[] DbServices = { "UsersService", "AuthService", "AccountsService", "TransactionsService" };
        static readonly string[] NoDbServices = { "AadharService", "CompanyCrvService", "NotificationService", "CentralPaymentGatewayService", "RegistryService", "CentralGatewayService" };
        static readonly string[] AllServices = DbServices.Concat(NoDbServices).ToArray();

        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 1;
            }

            string command = args[0].ToLowerInvariant();
            try
            {
                switch (command)
                {
                    case "check-reqs":
                        return CheckReqs();
                    case "all":
                        return SetupAll();
                    case "env":
                        return SetupEnv(args.Skip(1).ToArray());
                    case "provider":
                        return SetupProvider(args.Skip(1).ToArray());
                    case "frontend-env":
                        return FrontendEnv(args.Skip(1).ToArray());
                    case "mysql":
                        return SetupMySql(args.Skip(1).ToArray());
                    case "supabase":
                        return SetupSupabase(args.Skip(1).ToArray());
                    case "supabase-docker":
                        return SetupSupabaseDocker(args.Skip(1).ToArray());
                    case "sqlserver":
                        return SetupSqlServer(args.Skip(1).ToArray());
                    case "postgres":
                        return SetupPostgres(args.Skip(1).ToArray());
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        PrintHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("GDB Setup Tool");
            Console.WriteLine("Commands:");
            Console.WriteLine("  check-reqs                      - Check prerequisites (dotnet, node, npm)");
            Console.WriteLine("  all                             - Restore deps + auto-generate all env files");
            Console.WriteLine("  frontend-env [force]            - (Re)generate frontend/.env + .env.docker");
            Console.WriteLine("  env [pwd] [usr] [host] [port]   - Update local postgres credentials");
            Console.WriteLine("  provider <sqlite|inmemory>      - Set database provider to local/no-server");
            Console.WriteLine("  mysql <url>                     - Set database provider to MySQL");
            Console.WriteLine("  sqlserver <url>                 - Set database provider to SQL Server");
            Console.WriteLine("  supabase <url>                  - Set database provider to Supabase");
            Console.WriteLine("  supabase-docker <url>           - Create root .env for docker compose");
        }

        static int CheckReqs()
        {
            Console.WriteLine("Checking requirements...");
            bool missing = false;

            if (!CheckCommand("dotnet", "--version", ".NET SDK")) missing = true;
            if (!CheckCommand("node", "--version", "Node.js")) missing = true;
            if (!CheckCommand("npm", "--version", "npm")) missing = true;

            if (missing)
            {
                Console.WriteLine("\nMissing requirements. Please install them to proceed.");
                return 1;
            }
            Console.WriteLine("All requirements are satisfied.");
            return 0;
        }

        static bool CheckCommand(string cmd, string args, string name)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process!.WaitForExit();
                if (process.ExitCode == 0) return true;
            }
            catch { }
            Console.WriteLine($"   - {name} is not installed or not in PATH.");
            return false;
        }

        static int SetupAll()
        {
            Console.WriteLine("Restoring .NET solution dependencies...");
            var process = Process.Start("dotnet", "restore");
            process!.WaitForExit();
            bool backendOk = process.ExitCode == 0;
            Console.WriteLine(backendOk ? "✅ .NET Requirements installed" : "❌ Failed to restore .NET requirements");

            // Env files are AUTO-GENERATED here — no manual creation required.
            EnsureBackendEnv();       // seeds appsettings.Development.json (inmemory) if none exist
            WriteFrontendEnvFiles();  // writes frontend/.env (+ .env.docker) with service URLs

            string frontendDir = Path.Combine(GetRoot(), "frontend");
            bool frontendOk = false;
            if (File.Exists(Path.Combine(frontendDir, "package.json")))
            {
                Console.WriteLine("\n[frontend] Installing npm dependencies...");
                var npmProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "npm.cmd" : "npm",
                    Arguments = "install",
                    WorkingDirectory = frontendDir,
                    UseShellExecute = true
                });
                npmProcess!.WaitForExit();
                frontendOk = npmProcess.ExitCode == 0;
                Console.WriteLine(frontendOk ? "✅ [frontend] npm dependencies installed" : "❌ [frontend] npm install failed");
            }

            Console.WriteLine("\nSetup Summary:");
            Console.WriteLine($"   ✓ Backend Configured: {(backendOk ? "YES" : "NO")}");
            Console.WriteLine($"   ✓ Frontend Configured: {(frontendOk ? "YES" : "NO")}");
            return (backendOk && frontendOk) ? 0 : 1;
        }

        static int SetupEnv(string[] args)
        {
            string pwd = args.Length > 0 ? args[0] : "postgres";
            string usr = args.Length > 1 ? args[1] : "postgres";
            string host = args.Length > 2 ? args[2] : "localhost";
            string port = args.Length > 3 ? args[3] : "5432";

            Console.WriteLine($"GDB → Creating environment configurations (DB: {usr}:***@{host}:{port})");

            foreach (var svc in AllServices)
            {
                if (DbServices.Contains(svc) || svc == "AuthService" || svc == "UsersService")
                {
                    UpdateAppSettings(svc, node =>
                    {
                        node["DatabaseProvider"] = "postgres";
                        node["DatabaseUrl"] = "";
                        node["DatabaseUser"] = usr;
                        node["DatabasePassword"] = pwd;
                        node["DatabaseHost"] = host;
                        node["DatabasePort"] = int.Parse(port);
                    });
                }
                else
                {
                    UpdateAppSettings(svc, node => { });
                }
            }
            return 0;
        }

        static int SetupProvider(string[] args)
        {
            if (args.Length == 0 || (args[0] != "sqlite" && args[0] != "inmemory"))
            {
                Console.WriteLine("Usage: provider <sqlite|inmemory>");
                return 1;
            }
            string provider = args[0];
            Console.WriteLine($"GDB → {provider.ToUpper()} (no server needed)");

            foreach (var svc in DbServices)
            {
                UpdateAppSettings(svc, node =>
                {
                    node["DatabaseProvider"] = provider;
                    node["DatabaseUrl"] = "";
                });
            }
            foreach (var svc in NoDbServices)
            {
                UpdateAppSettings(svc, node => { });
            }
            return 0;
        }

        static int SetupMySql(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: mysql <url>");
                return 1;
            }
            string url = args[0];
            // Match Python parsing for SQLAlchemy URLs (e.g. mysql://root:pass@localhost:3306/gdb)
            var uri = new Uri(url.Replace("mysql+pymysql://", "mysql://"));
            var userInfo = uri.UserInfo.Split(':');
            string user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "root";
            string password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            string db = uri.AbsolutePath.TrimStart('/');
            int port = uri.Port > 0 ? uri.Port : 3306;

            // ADO.NET connection string for Pomelo.EntityFrameworkCore.MySql
            string adoConnectionString = $"Server={uri.Host};Port={port};Database={db};User={user};Password={password};";

            Console.WriteLine("GDB → MySQL setup");

            foreach (var svc in DbServices)
            {
                UpdateAppSettings(svc, node =>
                {
                    node["DatabaseProvider"] = "mysql";
                    node["DatabaseUrl"] = "";
                    node["DatabaseUser"] = user;
                    node["DatabasePassword"] = password;
                    node["DatabaseHost"] = uri.Host;
                    node["DatabasePort"] = port;
                    node["DatabaseName"] = $"gdb_{svc.ToLower().Replace("service", "")}_db";
                });
            }
            foreach (var svc in NoDbServices)
            {
                UpdateAppSettings(svc, node => { });
            }
            return 0;
        }

        static int SetupSqlServer(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: sqlserver <url>");
                return 1;
            }
            string url = args[0];
            var uri = new Uri(url.Replace("mssql://", "sqlserver://"));
            var userInfo = uri.UserInfo.Split(':');
            string user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "sa";
            string password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            int port = uri.Port > 0 ? uri.Port : 1433;

            Console.WriteLine("GDB → SQL Server setup");

            foreach (var svc in DbServices)
            {
                UpdateAppSettings(svc, node =>
                {
                    node["DatabaseProvider"] = "sqlserver";
                    node["DatabaseUrl"] = "";
                    node["DatabaseUser"] = user;
                    node["DatabasePassword"] = password;
                    node["DatabaseHost"] = uri.Host;
                    node["DatabasePort"] = port;
                    node["DatabaseName"] = $"gdb_{svc.ToLower().Replace("service", "")}_db";
                });
            }
            foreach (var svc in NoDbServices)
            {
                UpdateAppSettings(svc, node => { });
            }
            return 0;
        }

        static int SetupPostgres(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: postgres <url>");
                return 1;
            }
            string url = args[0];
            var uri = new Uri(url.Replace("postgresql://", "postgres://"));
            var userInfo = uri.UserInfo.Split(':');
            string user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
            string password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            int port = uri.Port > 0 ? uri.Port : 5432;

            Console.WriteLine("GDB → PostgreSQL setup");

            foreach (var svc in DbServices)
            {
                UpdateAppSettings(svc, node =>
                {
                    node["DatabaseProvider"] = "postgres";
                    node["DatabaseUrl"] = "";
                    node["DatabaseUser"] = user;
                    node["DatabasePassword"] = password;
                    node["DatabaseHost"] = uri.Host;
                    node["DatabasePort"] = port;
                    node["DatabaseName"] = $"gdb_{svc.ToLower().Replace("service", "")}_db";
                });
            }
            foreach (var svc in NoDbServices)
            {
                UpdateAppSettings(svc, node => { });
            }
            return 0;
        }

        static int SetupSupabase(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: supabase <url>");
                return 1;
            }
            string url = args[0];
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            string user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
            string password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            string db = uri.AbsolutePath.TrimStart('/');

            Console.WriteLine("GDB → Supabase setup");

            foreach (var svc in DbServices)
            {
                UpdateAppSettings(svc, node =>
                {
                    node["DatabaseProvider"] = "supabase";
                    node["DatabaseUrl"] = "";
                    node["DatabaseHost"] = uri.Host;
                    node["DatabasePort"] = uri.Port;
                    node["DatabaseUser"] = user;
                    node["DatabasePassword"] = password;
                    node["DatabaseName"] = db;
                });
            }

            foreach (var svc in NoDbServices)
            {
                UpdateAppSettings(svc, node => { });
            }
            return 0;
        }

        static int SetupSupabaseDocker(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: supabase-docker <url>");
                return 1;
            }
            string url = args[0];
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            string user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
            string password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            string db = uri.AbsolutePath.TrimStart('/');

            string content = $@"# Supabase-over-Docker settings
SUPABASE_DATABASE_URL={url}
SUPABASE_DB_HOST={uri.Host}
SUPABASE_DB_PORT={uri.Port}
SUPABASE_DB_USER={user}
SUPABASE_DB_PASSWORD={password}
SUPABASE_DB_NAME={db}
";
            File.WriteAllText(Path.Combine(GetRoot(), ".env"), content);
            Console.WriteLine("✅ Wrote .env for Docker Supabase");
            return 0;
        }

        static string GetRoot()
        {
            string? current = Directory.GetCurrentDirectory();
            while (current != null)
            {
                if (File.Exists(Path.Combine(current, "gdb-service-dotnet.slnx")) || File.Exists(Path.Combine(current, "gdb-service-dotnet.sln")))
                    return current;
                current = Directory.GetParent(current)?.FullName;
            }
            throw new Exception("Could not find solution root.");
        }

        // Seed appsettings.Development.json (backend "env") with a safe inmemory default for any
        // service that doesn't have one yet, so the stack boots right after `setup` — no manual config.
        static void EnsureBackendEnv()
        {
            Console.WriteLine("Ensuring backend env (appsettings.Development.json) exists...");
            foreach (var svc in DbServices)
            {
                if (!File.Exists(Path.Combine(GetRoot(), svc, "appsettings.Development.json")))
                    UpdateAppSettings(svc, node => { node["DatabaseProvider"] = "inmemory"; node["DatabaseUrl"] = ""; });
            }
            foreach (var svc in NoDbServices)
            {
                if (!File.Exists(Path.Combine(GetRoot(), svc, "appsettings.Development.json")))
                    UpdateAppSettings(svc, node => { });
            }
        }

        // Auto-generate the frontend env files (gitignored, hence missing on a fresh clone).
        //   .env        -> local `npm run dev`
        //   .env.docker -> Docker image build (`vite build --mode docker`)
        // Both point at host localhost:800x (the browser runs on the host; compose publishes those ports).
        static void WriteFrontendEnvFiles(bool force = false)
        {
            string frontendDir = Path.Combine(GetRoot(), "frontend");
            if (!File.Exists(Path.Combine(frontendDir, "package.json"))) return;

            string content =
@"# AUTO-GENERATED by Gdb.Setup — regenerate with: dotnet run --project tools/Gdb.Setup -- frontend-env
# Browser-facing base URL per backend service (the app appends paths like /api/v1/...).
VITE_AUTH_SERVICE_URL=http://localhost:8004
VITE_USERS_SERVICE_URL=http://localhost:8003
VITE_ACCOUNTS_SERVICE_URL=http://localhost:8001
VITE_TRANSACTIONS_SERVICE_URL=http://localhost:8002
VITE_AADHAR_SERVICE_URL=http://localhost:8005
VITE_COMPANY_CRV_SERVICE_URL=http://localhost:8006
VITE_NOTIFICATION_SERVICE_URL=http://localhost:8007
VITE_PAYMENT_GATEWAY_URL=http://localhost:8008
";
            foreach (var name in new[] { ".env", ".env.docker" })
            {
                string path = Path.Combine(frontendDir, name);
                if (force || !File.Exists(path))
                {
                    File.WriteAllText(path, content);
                    Console.WriteLine($"✅ [frontend] {(force ? "regenerated" : "generated")} {name}");
                }
                else
                {
                    Console.WriteLine($"↩️  [frontend] {name} already exists — left as is (use `frontend-env force` to overwrite)");
                }
            }
        }

        static int FrontendEnv(string[] args)
        {
            bool force = args.Length > 0 && (args[0].Equals("force", StringComparison.OrdinalIgnoreCase) || args[0] == "--force");
            WriteFrontendEnvFiles(force);
            return 0;
        }

        static void UpdateAppSettings(string service, Action<JsonObject> updateAction)
        {
            string svcDir = Path.Combine(GetRoot(), service);
            string appsettingsPath = Path.Combine(svcDir, "appsettings.json");
            string envPath = Path.Combine(svcDir, "appsettings.Development.json");

            if (!File.Exists(appsettingsPath))
            {
                Console.WriteLine($"⚠️  [{service}] appsettings.json not found - skipped");
                return;
            }

            string src = File.Exists(envPath) ? envPath : appsettingsPath;
            var jsonNode = JsonNode.Parse(File.ReadAllText(src)) as JsonObject;

            updateAction(jsonNode ?? throw new InvalidOperationException($"{src} is not a JSON object"));

            // appsettings.Development.json IS the local-development opt-in. The committed base
            // appsettings.json is fail-closed (AllowInsecureDefaults=false, secrets empty) so a
            // production host refuses to boot on dev keys; the generated Development file — never
            // committed, only ever loaded under ASPNETCORE_ENVIRONMENT=Development — enables the
            // shared dev fallbacks so the teaching stack starts with zero manual secret setup.
            // Upserted on every write so existing files pick it up on the next setup/provider run.
            jsonNode["AllowInsecureDefaults"] = true;

            File.WriteAllText(envPath, jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"✅ [{service}] config written");
        }
    }
}
