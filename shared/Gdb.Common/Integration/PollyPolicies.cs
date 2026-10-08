using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Gdb.Common.Integration;

public static class PollyPolicies
{
    /// <summary>
    /// Per-client policy selector: every attempt is bounded by a per-try timeout and goes through
    /// the client's circuit breaker; transient failures are RETRIED only for idempotent requests
    /// (GET/HEAD/OPTIONS). The <c>HttpClient.Timeout</c> on the client stays the overall budget.
    /// </summary>
    /// <remarks>
    /// Replaying a POST such as <c>/debit</c> or <c>/credit</c> after a lost response would move
    /// money again, so mutations get the breaker + timeout without the retry. Call this ONCE per
    /// <c>AddHttpClient</c> registration so each client owns a single breaker — breaker state must
    /// be shared across requests but not across unrelated dependencies.
    /// </remarks>
    public static Func<HttpRequestMessage, IAsyncPolicy<HttpResponseMessage>> CreateSelector(int perTryTimeoutSeconds = 4)
    {
        // Innermost: a hung dependency fails THIS attempt fast instead of eating the whole budget.
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(perTryTimeoutSeconds));

        var breaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 5, durationOfBreak: TimeSpan.FromSeconds(30));

        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)));

        var breakerAndTimeout = Policy.WrapAsync(breaker, timeout);            // breaker (outer) -> timeout (inner)
        var retryBreakerAndTimeout = Policy.WrapAsync(retry, breakerAndTimeout); // retry (outer) -> breaker -> timeout

        return request => IsIdempotent(request) ? retryBreakerAndTimeout : breakerAndTimeout;
    }

    private static bool IsIdempotent(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get || request.Method == HttpMethod.Head || request.Method == HttpMethod.Options;
}
