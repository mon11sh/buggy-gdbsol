namespace Gdb.Common.Exceptions;

/// <summary>
/// Base for every EXPECTED, client-facing failure. It carries the machine-readable code and the
/// HTTP status it maps to, so one shared <see cref="Middleware.ExceptionHandlingMiddleware"/>
/// serialises all of them identically — no per-service middleware, no per-service ErrorResponse.
/// Anything not deriving from this is an unexpected error and is answered as an opaque 500.
/// </summary>
public abstract class GdbException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    /// <summary>When set, emitted as the <c>Retry-After</c> header (seconds).</summary>
    public int? RetryAfterSeconds { get; init; }

    protected GdbException(string message, string errorCode, int statusCode = 400) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
