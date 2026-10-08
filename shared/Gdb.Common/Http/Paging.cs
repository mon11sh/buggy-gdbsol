namespace Gdb.Common.Http;

/// <summary>
/// Hard bounds for every list endpoint: a client can neither ask for a negative offset nor pull an
/// unbounded page (memory/latency ceiling per request is fixed by <see cref="MaxPageSize"/>).
/// </summary>
public static class Paging
{
    public const int MaxPageSize = 1000;
    public const string TotalCountHeader = "X-Total-Count";

    public static (int Skip, int Limit) Clamp(int skip, int? limit) =>
        (Math.Max(0, skip), Math.Clamp(limit ?? MaxPageSize, 1, MaxPageSize));
}
