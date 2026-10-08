using System.Net;
using System.Net.Http.Json;

namespace Gdb.Integration.Tests;

[TestClass]
public class AuthenticationTests
{
    private static AuthHost _auth = null!;

    [ClassInitialize]
    public static void Boot(TestContext _) => _auth = new AuthHost();

    [ClassCleanup]
    public static void Shutdown() => _auth.Dispose();

    [TestMethod]
    public async Task Valid_credentials_return_a_bearer_token_with_the_role()
    {
        var response = await _auth.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { login_id = "teller", password = AuthHost.Password });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Json();
        Assert.IsFalse(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
        Assert.AreEqual("bearer", body.GetProperty("token_type").GetString()!.ToLowerInvariant());
        Assert.AreEqual("TELLER", body.GetProperty("role").GetString());
    }

    [TestMethod]
    public async Task Wrong_password_is_401_with_the_shared_error_contract()
    {
        var response = await _auth.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { login_id = "admin", password = "nope" });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Json();
        Assert.AreEqual("INVALID_CREDENTIALS", body.GetProperty("error_code").GetString());
        Assert.IsFalse(string.IsNullOrEmpty(body.GetProperty("message").GetString()));
        Assert.IsFalse(string.IsNullOrEmpty(body.GetProperty("correlation_id").GetString()), "error bodies carry the correlation id");
        Assert.IsTrue(response.Headers.Contains("X-Correlation-ID"), "every response echoes the correlation id");
    }

    [TestMethod]
    public async Task Five_failures_throttle_the_login_id()
    {
        var client = _auth.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new { login_id = "manager", password = "wrong" });
            Assert.AreEqual(HttpStatusCode.Unauthorized, failed.StatusCode, $"attempt {i + 1}");
        }

        var blocked = await client.PostAsJsonAsync("/api/v1/auth/login", new { login_id = "manager", password = AuthHost.Password });

        Assert.AreEqual(HttpStatusCode.TooManyRequests, blocked.StatusCode, "even the right password is refused while locked");
        Assert.AreEqual("TOO_MANY_ATTEMPTS", (await blocked.Json()).GetProperty("error_code").GetString());
    }

    [TestMethod]
    public async Task Unknown_user_is_indistinguishable_from_a_wrong_password()
    {
        var response = await _auth.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { login_id = "nobody", password = AuthHost.Password });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual("INVALID_CREDENTIALS", (await response.Json()).GetProperty("error_code").GetString());
    }
}
