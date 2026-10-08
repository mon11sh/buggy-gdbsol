using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Gdb.OpenApiGate
{
    class Program
    {
        static void Main(string[] args)
        {
            bool update = args.Contains("--update");
            var named = args.Where(a => !a.StartsWith("-")).ToList();

            string root = GetRoot();
            string snapDir = Path.Combine(root, "openapi-snapshots");

            // Build the whole solution ONCE up front and start each service with --no-build. Spawning a
            // plain `dotnet run` per service re-restores and re-builds every time (minutes each, x10).
            if (!args.Contains("--no-solution-build"))
            {
                Console.WriteLine("Building solution once (services are then started with --no-build)...");
                var build = Process.Start(new ProcessStartInfo("dotnet",
                    $"build \"{Path.Combine(root, "gdb-service-dotnet.slnx")}\" -v q --nologo")
                { UseShellExecute = false });
                build!.WaitForExit();
                if (build.ExitCode != 0)
                {
                    Console.WriteLine("Solution build failed; cannot capture OpenAPI documents.");
                    Environment.Exit(1);
                }
            }
            Directory.CreateDirectory(snapDir);

            var services = Directory.GetDirectories(root, "*Service")
                .Where(d => File.Exists(Path.Combine(d, "Program.cs")))
                .ToList();

            if (named.Any())
                services = services.Where(s => named.Contains(Path.GetFileName(s))).ToList();

            int failures = 0;

            foreach (var svcDir in services)
            {
                string name = Path.GetFileName(svcDir);
                string pyName = string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLowerInvariant();
                string snapPath = Path.Combine(snapDir, $"{pyName}.json");

                try
                {
                    var current = GenerateOpenApi(svcDir, name);
                    if (current == null)
                    {
                        // Never write or compare a document we could not capture: an empty baseline would
                        // silently disable contract protection for this service.
                        Console.WriteLine($"[capture] {name}: FAIL (service did not serve its OpenAPI document)");
                        failures++;
                        continue;
                    }
                    
                    if (update)
                    {
                        File.WriteAllText(snapPath, current);
                        Console.WriteLine($"[update] {name}: snapshot written");
                        continue;
                    }

                    if (!File.Exists(snapPath))
                    {
                        Console.WriteLine($"[check] {name}: NO SNAPSHOT (run --update to create the baseline)");
                        File.WriteAllText(snapPath.Replace(".json", "_actual.json"), current);
                        failures++;
                        continue;
                    }

                    string stored = File.ReadAllText(snapPath);
                    File.WriteAllText(snapPath.Replace(".json", "_actual.json"), current);
                    
                    if (SemanticDiff.AreEqual(stored, current, out var diffReason))
                    {
                        Console.WriteLine($"[check] {name}: OK (OpenAPI matches semantically)");
                    }
                    else
                    {
                        Console.WriteLine($"[check] {name}: FAIL (OpenAPI changed vs snapshot)");
                        Console.WriteLine($"      -> {diffReason}");
                        failures++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[check] {name}: ERROR ({e.Message})");
                    failures++;
                }
            }

            if (update)
            {
                if (failures > 0)
                {
                    Console.WriteLine($"\n{failures} service(s) could not be captured; their snapshots were left untouched.");
                    Environment.Exit(1);
                }
                Console.WriteLine("\nSnapshots updated.");
                return;
            }

            if (failures > 0)
            {
                Console.WriteLine($"\n{failures} service(s) failed the OpenAPI gate.");
                Environment.Exit(1);
            }

            Console.WriteLine("\nOpenAPI gate: all OK.");
        }

        static string? GenerateOpenApi(string svcDir, string name)
        {
            int port = GetPortForService(name);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-launch-profile --no-build --no-restore",
                WorkingDirectory = svcDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            psi.EnvironmentVariables["DatabaseProvider"] = "inmemory";
            // The services are fail-closed and serve Swagger only in Development / with EnableSwagger:
            // run them as a throw-away dev instance so the contract can be captured (no real secrets involved).
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.EnvironmentVariables["AllowInsecureDefaults"] = "true";
            psi.EnvironmentVariables["EnableSwagger"] = "true";
            psi.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";

            using var process = Process.Start(psi)!;

            // Drain BOTH redirected streams continuously. Reading them only after exit is the classic
            // redirected-output deadlock: a chatty service (e.g. one that logs while seeding) fills the
            // pipe buffer and blocks inside Console.Write before it ever binds its port, so the poll
            // below times out and the capture "mysteriously" fails.
            var stdout = new System.Text.StringBuilder();
            var stderr = new System.Text.StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                
                string url = $"http://127.0.0.1:{port}/swagger/v1/swagger.json";
                string? content = null;
                
                // Poll for up to 3 minutes: `dotnet run --no-build` still runs MSBuild up-to-date checks and a
                // service start can take well over a minute on a slow/synced disk. A timeout is a FAILURE
                // (see below), never a reason to record a degraded contract.
                for (int i = 0; i < 360; i++)
                {
                    if (process.HasExited) break;
                    try
                    {
                        content = client.GetStringAsync(url).Result;
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }

                if (content == null)
                {
                    Console.WriteLine(process.HasExited
                        ? $"[DEBUG] Process exited early for {name}. Exit Code: {process.ExitCode}"
                        : $"[DEBUG] Timed out waiting for {name}.");
                    string Tail(System.Text.StringBuilder sb) { lock (sb) { var s = sb.ToString(); return s.Length > 4000 ? s[^4000..] : s; } }
                    Console.WriteLine($"[DEBUG] STDOUT (tail): {Tail(stdout)}");
                    Console.WriteLine($"[DEBUG] STDERR (tail): {Tail(stderr)}");
                }

                if (content != null)
                {
                    var json = JsonDocument.Parse(content);
                    return JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
                }
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            
            // Capture failed (timeout or early exit). Returning a stub document here used to let
            // `--update` commit an EMPTY contract as the baseline, so later drift went undetected.
            return null;
        }

        static int GetPortForService(string name)
        {
            return name switch
            {
                "AuthService" => 18004,
                "UsersService" => 18003,
                "AccountsService" => 18001,
                "TransactionsService" => 18002,
                "AadharService" => 18005,
                "CompanyCrvService" => 18006,
                "NotificationService" => 18007,
                "CentralPaymentGatewayService" => 18008,
                "RegistryService" => 18010,
                "CentralGatewayService" => 18000,
                _ => 18099
            };
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
