using System.Net;
using System.Net.Http.Json;

namespace Gdb.Integration.Tests;

/// <summary>
/// Tokens minted by the real AuthService are presented to the real AccountsService: deny-by-default,
/// role checks, the error contract and the PIN lockout are exercised over HTTP.
/// </summary>
[TestClass]
public class AuthorizationAndErrorContractTests
{
    private static AuthHost _auth = null!;
    private static AccountsHost _accounts = null!;
    private static string _admin = null!;
    private static string _manager = null!;

    [ClassInitialize]
    public static async Task Boot(TestContext _)
    {
        _auth = new AuthHost();
        _accounts = new AccountsHost();
        _admin = await _auth.TokenAsync("admin");
        _manager = await _auth.TokenAsync("manager");
    }

    [ClassCleanup]
    public static void Shutdown()
    {
        _accounts.Dispose();
        _auth.Dispose();
    }

    [TestMethod]
    public async Task No_token_is_401_everywhere_except_liveness()
    {
        var client = _accounts.CreateClient();

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts/1000")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/live")).StatusCode, "probes must not need a token");
    }

    [TestMethod]
    public async Task Role_is_enforced_from_the_token()
    {
        var manager = _accounts.CreateClient().WithToken(_manager);
        var admin = _accounts.CreateClient().WithToken(_admin);

        var denied = await manager.PostAsync("/api/v1/accounts/1000/activate", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode, "activate is ADMIN-only");

        var listed = await manager.GetAsync("/api/v1/accounts");
        Assert.AreEqual(HttpStatusCode.OK, listed.StatusCode, "MANAGER may read");
        Assert.IsTrue(listed.Headers.Contains("X-Total-Count"));

        var account = await admin.GetAsync("/api/v1/accounts/1000");
        Assert.AreEqual(HttpStatusCode.OK, account.StatusCode);
        var body = await account.Json();
        Assert.AreEqual(1000, body.GetProperty("account_number").GetInt32());
        Assert.AreEqual("SAVINGS", body.GetProperty("account_type").GetString());
        Assert.StartsWith("****", body.GetProperty("aadhar_number").GetString()!, "Aadhaar is decrypted then masked");
    }

    [TestMethod]
    public async Task Domain_errors_use_the_shared_contract_with_a_correlation_id()
    {
        var response = await _accounts.CreateClient().WithToken(_admin).GetAsync("/api/v1/accounts/999999");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Json();
        Assert.AreEqual("ACCOUNT_NOT_FOUND", body.GetProperty("error_code").GetString());
        Assert.AreEqual(body.GetProperty("message").GetString(), body.GetProperty("detail").GetString());
        Assert.AreEqual("error", body.GetProperty("status").GetString());
        Assert.AreEqual(response.Headers.GetValues("X-Correlation-ID").Single(), body.GetProperty("correlation_id").GetString());
        Assert.IsTrue(response.Headers.Contains("X-Content-Type-Options"), "security headers reach error responses too");
    }

    [TestMethod]
    public async Task Five_wrong_pins_lock_the_account_with_retry_after()
    {
        var client = _accounts.CreateClient().WithToken(_admin);
        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/v1/accounts/1001/verify-pin", new { pin = "9999" });
            Assert.AreEqual(HttpStatusCode.Unauthorized, wrong.StatusCode, $"attempt {i + 1}");
            Assert.AreEqual("INVALID_PIN", (await wrong.Json()).GetProperty("error_code").GetString());
        }

        var locked = await client.PostAsJsonAsync("/api/v1/accounts/1001/verify-pin", new { pin = "1234" });

        Assert.AreEqual((HttpStatusCode)423, locked.StatusCode, "the correct PIN is refused while locked");
        Assert.AreEqual("ACCOUNT_LOCKED", (await locked.Json()).GetProperty("error_code").GetString());
        Assert.IsNotNull(locked.Headers.RetryAfter, "Retry-After tells the client when to come back");
    }
}
