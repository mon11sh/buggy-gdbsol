extern alias Auth;
extern alias Accounts;
extern alias Transactions;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Auth::AuthService.Domain.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Transactions::TransactionsService.Integration;

namespace Gdb.Integration.Tests;

/// <summary>Every host runs as a throw-away Development instance on the in-memory provider.</summary>
internal static class HostSettings
{
    public static void Apply(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("AllowInsecureDefaults", "true");
        builder.UseSetting("DatabaseProvider", "inmemory");
        builder.UseSetting("ServiceDiscoveryEnabled", "false");
    }
}

/// <summary>AuthService with the UsersService lookup faked: admin / teller / manager, password Welcome@1.</summary>
public sealed class AuthHost : WebApplicationFactory<Auth::Program>
{
    public const string Password = "Welcome@1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        HostSettings.Apply(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserServicePort>();
            services.AddSingleton<IUserServicePort>(new FakeUsersPort());
        });
    }

    public async Task<string> TokenAsync(string loginId)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { login_id = loginId, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Json();
        return body.GetProperty("access_token").GetString()!;
    }

    private sealed class FakeUsersPort : IUserServicePort
    {
        private static readonly Dictionary<string, (int Id, string Role)> Users = new()
        {
            ["admin"] = (1, "ADMIN"),
            ["teller"] = (2, "TELLER"),
            ["manager"] = (3, "MANAGER"),
        };

        public Task<Dictionary<string, object>?> VerifyUserCredentialsAsync(string loginId, string password, CancellationToken ct = default)
        {
            if (password == Password && Users.TryGetValue(loginId, out var user))
                return Task.FromResult<Dictionary<string, object>?>(new()
                {
                    ["is_valid"] = true, ["user_id"] = user.Id, ["role"] = user.Role, ["is_active"] = true,
                });
            return Task.FromResult<Dictionary<string, object>?>(new() { ["is_valid"] = false });
        }
    }
}

/// <summary>AccountsService as-is (seeds Savings #1000 and Current #1001, PIN 1234).</summary>
public sealed class AccountsHost : WebApplicationFactory<Accounts::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => HostSettings.Apply(builder);
}

/// <summary>
/// TransactionsService with its three outbound edges (Accounts internal API, payment gateway,
/// notifications) answered by <see cref="Edges"/>, so every saga failure mode can be forced.
/// </summary>
public sealed class TransactionsHost : WebApplicationFactory<Transactions::Program>
{
    public FakeEdges Edges { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        HostSettings.Apply(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<AccountServiceClient>().ConfigurePrimaryHttpMessageHandler(() => Edges);
            services.AddHttpClient<PaymentGatewayClient>().ConfigurePrimaryHttpMessageHandler(() => Edges);
            services.AddHttpClient<NotificationClient>().ConfigurePrimaryHttpMessageHandler(() => Edges);
        });
    }
}

/// <summary>Scripted stand-in for AccountsService' internal API, the payment gateway and notifications.</summary>
public sealed class FakeEdges : HttpMessageHandler
{
    public const int Savings = 1000;
    public const int Current = 1001;
    public const string Pin = "1234";

    public ConcurrentQueue<(string Method, string Path)> Calls { get; } = new();

    /// <summary>Decides how a credit to the given account is answered (default: success).</summary>
    public Func<int, HttpStatusCode> CreditStatus { get; set; } = _ => HttpStatusCode.OK;

    public int Count(string method, string pathSuffix) => Calls.Count(c => c.Method == method && c.Path.EndsWith(pathSuffix, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        Calls.Enqueue((request.Method.Method, path));
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

        if (path == "/api/v1/payment/process") return Json(HttpStatusCode.OK, """{"success":true}""");
        if (path == "/api/v1/notify/send") return Json(HttpStatusCode.OK, "{}");

        const string prefix = "/api/v1/internal/accounts/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return Json(HttpStatusCode.NotFound, "{}");

        var rest = path[prefix.Length..].Split('/', 2);
        var account = int.Parse(rest[0]);
        var action = rest.Length > 1 ? rest[1] : "";
        var known = account is Savings or Current;

        return action switch
        {
            "" => known
                ? Json(HttpStatusCode.OK, """{"is_active":true,"balance":100000,"privilege":"GOLD"}""")
                : Json(HttpStatusCode.NotFound, """{"error_code":"ACCOUNT_NOT_FOUND","message":"not found"}"""),
            "verify-pin" => body.Contains($"\"{Pin}\"")
                ? Json(HttpStatusCode.OK, """{"pin_valid":true}""")
                : Json(HttpStatusCode.Unauthorized, """{"error_code":"INVALID_PIN","message":"Invalid PIN"}"""),
            "privilege" => Json(HttpStatusCode.OK, """{"privilege":"GOLD"}"""),
            "debit" => Json(HttpStatusCode.OK, """{"new_balance":99500}"""),
            "credit" => CreditStatus(account) is var status && status == HttpStatusCode.OK
                ? Json(HttpStatusCode.OK, """{"new_balance":100500}""")
                : Json(status, """{"error_code":"UPSTREAM_FAILURE","message":"simulated failure"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    protected override void Dispose(bool disposing) { /* shared by three client pipelines; nothing to release */ }
}

public static class HttpExtensions
{
    public static async Task<JsonElement> Json(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
