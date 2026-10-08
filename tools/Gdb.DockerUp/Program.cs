using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Gdb.DockerUp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run --project tools/Gdb.DockerUp -- <provider>");
                Console.WriteLine("Providers: inmemory, sqlite, postgres, mysql, sqlserver, supabase, down");
                Environment.Exit(1);
            }

            string provider = args[0].ToLowerInvariant();
            string root = GetRoot();

            if (provider == "down")
            {
                var downCmd = $"compose -f docker-compose.yml down {string.Join(" ", args.Skip(1))}";
                RunDocker(downCmd, root);
                return;
            }

            string[] validProviders = { "inmemory", "sqlite", "postgres", "mysql", "sqlserver", "supabase" };
            if (!validProviders.Contains(provider))
            {
                Console.WriteLine($"Unknown provider '{provider}'.");
                Environment.Exit(1);
            }

            if (provider == "supabase" && !File.Exists(Path.Combine(root, ".env")))
            {
                Console.WriteLine("Supabase needs your cloud credentials first:");
                Console.WriteLine("  1) copy supabase.env.example -> .env");
                Console.WriteLine("  2) fill in your Supabase connection values");
                Console.WriteLine("  3) re-run: dotnet run --project tools/Gdb.DockerUp -- supabase");
                Environment.Exit(1);
            }

            string cmd = "compose -f docker-compose.yml";
            if (provider != "inmemory")
            {
                cmd += $" -f docker-compose.{provider}.yml";
            }

            var passthrough = args.Skip(1).ToList();
            if (!passthrough.Contains("-d") && !passthrough.Contains("--detach") && !passthrough.Contains("--build"))
            {
                passthrough.Insert(0, "--build");
            }

            cmd += $" up {string.Join(" ", passthrough)}";

            Console.WriteLine($"→ docker {cmd}");
            Console.WriteLine($"   provider = {provider}   |   open http://localhost:3000  (admin / Welcome@1)\n");

            RunDocker(cmd, root);
        }

        static void RunDocker(string args, string cwd)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                WorkingDirectory = cwd,
                UseShellExecute = false
            };
            var process = Process.Start(psi)!;
            process.WaitForExit();
            Environment.Exit(process.ExitCode);
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
