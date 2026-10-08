using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gdb.Integration.Tests;

/// <summary>Refresh-token lifecycle: httpOnly cookie at login, rotation on refresh, replay refused, never usable as Bearer.</summary>
[TestClass]
public class RefreshTokenTests
{
    private const string CookieName = "gdb_refresh";
    private static AuthHost _auth = null!;
    private static AccountsHost _accounts = null!;

    [ClassInitialize]
    public static void Boot(TestContext _)
    {
        _auth = new AuthHost();
        _accounts = new AccountsHost();
    }

    [ClassCleanup]
    public static void Shutdown()
    {
        _accounts.Dispose();
        _auth.Dispose();
    }

    private static HttpClient RawClient(AuthHost host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string RefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal));
        StringAssert.Contains(setCookie.ToLowerInvariant(), "httponly", "the refresh token must be invisible to page scripts");
        StringAssert.Contains(setCookie.ToLowerInvariant(), "samesite=strict");
        return setCookie.Split(';')[0][(CookieName.Length + 1)..];
    }

    [TestMethod]
    public async Task Login_sets_an_httpOnly_refresh_cookie_and_refresh_rotates_it()
    {
        var client = RawClient(_auth);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { login_id = "teller", password = AuthHost.Password });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var firstAccess = (await login.Json()).GetProperty("access_token").GetString();
        var firstRefresh = RefreshCookie(login);

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("Cookie", $"{CookieName}={firstRefresh}");
        var refreshed = await client.SendAsync(refresh);

        Assert.AreEqual(HttpStatusCode.OK, refreshed.StatusCode, await refreshed.Content.ReadAsStringAsync());
        var secondAccess = (await refreshed.Json()).GetProperty("access_token").GetString();
        Assert.AreNotEqual(firstAccess, secondAccess, "a fresh access token is issued");
        Assert.AreNotEqual(firstRefresh, RefreshCookie(refreshed), "the refresh token is rotated");

        // The rotated-out token is dead: a replay (a stolen cookie used late) is refused and the cookie is cleared.
        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replay.Headers.Add("Cookie", $"{CookieName}={firstRefresh}");
        var refused = await client.SendAsync(replay);
        Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.AreEqual("INVALID_CREDENTIALS", (await refused.Json()).GetProperty("error_code").GetString());
    }

    [TestMethod]
    public async Task Refresh_token_in_the_body_works_for_non_browser_clients_and_logout_kills_it()
    {
        var client = RawClient(_auth);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { login_id = "manager", password = AuthHost.Password });
        var refreshToken = RefreshCookie(login);
        var access = (await login.Json()).GetProperty("access_token").GetString();

        var viaBody = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = refreshToken });
        Assert.AreEqual(HttpStatusCode.OK, viaBody.StatusCode);
        var rotated = RefreshCookie(viaBody);

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Authorization = new("Bearer", access);
        logout.Headers.Add("Cookie", $"{CookieName}={rotated}");
        Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(logout)).StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = rotated });
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterLogout.StatusCode, "logout revokes the refresh token too");
    }

    [TestMethod]
    public async Task A_refresh_token_is_never_accepted_as_a_bearer_token()
    {
        var login = await RawClient(_auth).PostAsJsonAsync("/api/v1/auth/login", new { login_id = "admin", password = AuthHost.Password });
        var refreshToken = RefreshCookie(login);

        var atAccounts = await _accounts.CreateClient().WithToken(refreshToken).GetAsync("/api/v1/accounts/1000");
        Assert.AreEqual(HttpStatusCode.Unauthorized, atAccounts.StatusCode, "resource services reject token_use=refresh");

        var atAuth = await _auth.CreateClient().WithToken(refreshToken).GetAsync("/api/v1/auth/verify");
        Assert.AreEqual(HttpStatusCode.Unauthorized, atAuth.StatusCode, "the auth service rejects it too");

        var missing = await RawClient(_auth).PostAsync("/api/v1/auth/refresh", null);
        Assert.AreEqual(HttpStatusCode.Unauthorized, missing.StatusCode, "no cookie, no body: nothing to refresh");
    }
}
