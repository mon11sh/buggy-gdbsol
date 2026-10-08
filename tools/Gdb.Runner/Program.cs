using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Gdb.Runner
{
    class Program
    {
        static readonly (string Name, int Port)[] Services =
        {
            ("AuthService", 8004),
            ("UsersService", 8003),
            ("AccountsService", 8001),
            ("AadharService", 8005),
            ("CompanyCrvService", 8006),
            ("NotificationService", 8007),
            ("CentralPaymentGatewayService", 8008),
            ("TransactionsService", 8002),
            ("RegistryService", 8010),
            ("CentralGatewayService", 8000)
        };

        static void Main(string[] args)
        {
            // Hot reload is OPT-IN. Running all 10 services under `dotnet watch` means 10
            // concurrent MSBuild instances, which OOMs a memory-tight machine (and takes the
            // frontend's esbuild down with it). Default = plain `dotnet run --no-build` (needs a
            // prior `dotnet build`; the `gdb local` wrapper does that build for you). The frontend
            // still hot-reloads via Vite HMR regardless. To watch the BACKEND too, set GDB_WATCH=1
            // (or pass --watch) — best on a roomy machine, or watch a single service by hand:
            // `cd <Service> && dotnet watch run`.
            var watchEnv = Environment.GetEnvironmentVariable("GDB_WATCH");
            bool hotReload = args.Contains("--watch")
                || string.Equals(watchEnv, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(watchEnv, "true", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"🚀 GDB (.NET) — Starting all services  (hot reload: {(hotReload ? "ON" : "OFF")})");
            Console.WriteLine(new string('=', 60));

            string root = GetRoot();

            if (!Directory.Exists(Path.Combine(root, "AccountsService", "bin")) &&
                !Directory.Exists(Path.Combine(root, "AccountsService", "obj")))
            {
                Console.WriteLine("❌ Missing build output. Did you run `dotnet run --project tools/Gdb.Setup -- all` first?");
                Environment.Exit(1);
            }

            foreach (var svc in Services)
            {
                string cwd = Path.Combine(root, svc.Name);
                // `dotnet watch` recompiles/restarts on file changes; plain run is a one-shot launch.
                string command = hotReload
                    ? $"dotnet watch --non-interactive run --urls http://0.0.0.0:{svc.Port}"
                    : $"dotnet run --no-build --urls http://0.0.0.0:{svc.Port}";
                Console.WriteLine($"▶️  [{svc.Name}]  http://localhost:{svc.Port}");

                Launch(svc.Name, command, cwd, svc.Name != "RegistryService", svc.Port);
                Thread.Sleep(2000); // Stagger startup
            }

            string frontend = Path.Combine(root, "frontend");
            if (File.Exists(Path.Combine(frontend, "package.json")))
            {
                // Vite dev server = hot module replacement out of the box (no restart needed).
                Console.WriteLine("▶️  [frontend] npm run dev  (Vite HMR)");
                Launch("frontend", "npm run dev", frontend, false, 3000);
            }

            Console.WriteLine(new string('=', 60));
            Console.WriteLine("✅ Launched. Each service is in its OWN window.");
            if (hotReload)
                Console.WriteLine("   ✏️  Backend hot reload ON (dotnet watch) — heavy on RAM; frontend uses Vite HMR.");
            else
                Console.WriteLine("   ✏️  Frontend hot-reloads (Vite HMR). Backend: edit + restart its window, or GDB_WATCH=1 to watch (RAM-heavy).");
            Console.WriteLine("   Swagger:  http://localhost:8001/api/v1/docs");
            Console.WriteLine("   Frontend: http://localhost:3000 or :5173");
            Console.WriteLine("   Login:    admin / Welcome@1");
            Console.WriteLine(new string('=', 60));
        }

        static void Launch(string title, string command, string cwd, bool enableDiscovery, int port)
        {
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = cwd,
                UseShellExecute = true
            };

            // Pin PORT + Host for EVERY child. Container/PaaS shells (code-server, Coder, Heroku-style)
            // often export a global PORT (e.g. 43831); .NET config binding is case-insensitive, so that
            // ambient PORT overrides every service's `Port` setting and they all try to bind the same
            // port -> "address already in use" crash-loop. Setting PORT per child overrides it so each
            // service binds its own port. Host=0.0.0.0 keeps them reachable through a container proxy.
            string winEnv = $"set PORT={port}& set Host=0.0.0.0& ";
            string shEnv  = $"PORT={port} Host=0.0.0.0 ";
            if (enableDiscovery)
            {
                winEnv += "set ServiceDiscoveryEnabled=true& set RegistryUrl=http://localhost:8010& ";
                shEnv  += "ServiceDiscoveryEnabled=true RegistryUrl=http://localhost:8010 ";
            }

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c start \"{title}\" cmd.exe /k \"title {title} & {winEnv}{command}\"";
            }
            else if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "osascript";
                psi.Arguments = $"-e 'tell application \"Terminal\" to do script \"cd {cwd} && {shEnv}{command}\"'";
            }
            else
            {
                // Linux (incl. the browser-based VS Code container students use): inline env assignments
                // apply only to this child process. Previously nothing was set here, so the ambient PORT
                // leaked into every service.
                psi.FileName = "bash";
                psi.Arguments = $"-c \"{shEnv}{command}\"";
            }

            Process.Start(psi);
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
    }
}
