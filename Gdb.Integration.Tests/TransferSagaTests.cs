using System.Net;
using System.Net.Http.Json;

namespace Gdb.Integration.Tests;

/// <summary>
/// The transfer saga through the real TransactionsService over HTTP, with the Accounts internal API
/// scripted per test. Each test gets its own host so one scenario's circuit-breaker state cannot
/// leak into the next.
/// </summary>
[TestClass]
public class TransferSagaTests
{
    private static AuthHost _auth = null!;
    private static string _teller = null!;
    private static string _admin = null!;

    [ClassInitialize]
    public static async Task Boot(TestContext _)
    {
        _auth = new AuthHost();
        _teller = await _auth.TokenAsync("teller");
        _admin = await _auth.TokenAsync("admin");
    }

    [ClassCleanup]
    public static void Shutdown() => _auth.Dispose();

    private static object Transfer(decimal amount = 500m, string pin = FakeEdges.Pin) =>
        new { from_account = FakeEdges.Savings, to_account = FakeEdges.Current, amount, pin, transfer_mode = "NEFT" };

    private static HttpRequestMessage Post(object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/transfer") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    [TestMethod]
    public async Task Happy_path_debits_credits_and_writes_both_ledger_legs()
    {
        using var host = new TransactionsHost();
        var client = host.CreateClient().WithToken(_teller);

        var response = await client.SendAsync(Post(Transfer(), Guid.NewGuid().ToString()));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Json();
        Assert.AreEqual("SUCCESS", body.GetProperty("status").GetString());
        Assert.AreEqual(1, host.Edges.Count("POST", "/1000/debit"));
        Assert.AreEqual(1, host.Edges.Count("POST", "/1001/credit"));

        var history = await client.GetAsync($"/api/v1/transactions/account/{FakeEdges.Savings}?type=TRANSFER");
        Assert.AreEqual(HttpStatusCode.OK, history.StatusCode);
        var logs = (await history.Json()).GetProperty("logs").EnumerateArray().ToList();
        Assert.IsTrue(logs.Any(l => l.GetProperty("amount").GetDecimal() == 500m), "the source leg is in the ledger");

        // One-to-many: the transfer owns both ledger legs (eager + split query through the real service).
        var transferId = body.GetProperty("transaction_id").GetInt32();
        var details = await client.GetAsync($"/api/v1/transactions/transfers/{transferId}");
        Assert.AreEqual(HttpStatusCode.OK, details.StatusCode);
        var legs = (await details.Json()).GetProperty("legs").EnumerateArray().ToList();
        Assert.HasCount(2, legs, "source debit + destination credit");
        CollectionAssert.AreEquivalent(new[] { FakeEdges.Savings, FakeEdges.Current }, legs.Select(l => l.GetProperty("account_number").GetInt32()).ToArray());

        var unknown = await client.GetAsync("/api/v1/transactions/transfers/999999");
        Assert.AreEqual(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [TestMethod]
    public async Task Credit_failure_refunds_the_source_and_reports_503()
    {
        using var host = new TransactionsHost();
        host.Edges.CreditStatus = account => account == FakeEdges.Current ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
        var client = host.CreateClient().WithToken(_teller);

        var response = await client.SendAsync(Post(Transfer(), Guid.NewGuid().ToString()));

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Json();
        Assert.AreEqual("SERVICE_UNAVAILABLE", body.GetProperty("error_code").GetString());
        StringAssert.Contains(body.GetProperty("message").GetString(), "reversed");
        Assert.AreEqual(1, host.Edges.Count("POST", "/1000/debit"));
        Assert.AreEqual(1, host.Edges.Count("POST", "/1001/credit"), "destination credit attempted once");
        Assert.AreEqual(1, host.Edges.Count("POST", "/1000/credit"), "source refunded exactly once");
    }

    [TestMethod]
    public async Task Credit_and_refund_failures_flag_reconciliation_and_block_replays()
    {
        using var host = new TransactionsHost();
        host.Edges.CreditStatus = _ => HttpStatusCode.InternalServerError; // destination AND refund fail
        var client = host.CreateClient().WithToken(_teller);
        var key = Guid.NewGuid().ToString();

        var response = await client.SendAsync(Post(Transfer(), key));

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        StringAssert.Contains((await response.Json()).GetProperty("message").GetString(), "reconciliation");
        Assert.AreEqual(1, host.Edges.Count("POST", "/1000/debit"));
        Assert.AreEqual(3, host.Edges.Count("POST", "/1000/credit"), "refund is retried three times");

        // Replaying the same key must NOT move money again: the failure was recorded against the key.
        var replay = await client.SendAsync(Post(Transfer(), key));
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, replay.StatusCode);
        Assert.AreEqual(1, host.Edges.Count("POST", "/1000/debit"), "no second debit on replay");
    }

    [TestMethod]
    public async Task Wrong_pin_stops_before_any_money_moves()
    {
        using var host = new TransactionsHost();
        var client = host.CreateClient().WithToken(_teller);

        var response = await client.SendAsync(Post(Transfer(pin: "0000")));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual("INVALID_PIN", (await response.Json()).GetProperty("error_code").GetString());
        Assert.AreEqual(0, host.Edges.Count("POST", "/1000/debit"));
    }

    [TestMethod]
    public async Task Transfers_are_for_tellers_and_managers_only()
    {
        using var host = new TransactionsHost();

        var asAdmin = await host.CreateClient().WithToken(_admin).SendAsync(Post(Transfer()));
        Assert.AreEqual(HttpStatusCode.Forbidden, asAdmin.StatusCode);

        var anonymous = await host.CreateClient().SendAsync(Post(Transfer()));
        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        Assert.IsEmpty(host.Edges.Calls, "nothing reaches the edges without authorization");
    }
}
