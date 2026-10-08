namespace Gdb.Common.Data;

/// <summary>
/// Explicit connection-pool bounds for every SQL Server connection string the services build.
/// ADO.NET pools connections per distinct connection string; making the bounds explicit keeps a
/// runaway caller from opening unbounded connections (Max) and keeps a warm floor for the first
/// requests after start-up (Min). EF Core reuses the same pool because it uses the same string.
/// </summary>
public static class ConnectionPooling
{
    public const int MinPoolSize = 2;
    public const int MaxPoolSize = 100;

    /// <summary>Append to a SQL Server connection string (already ends with ';').</summary>
    public static readonly string SqlServer = $"Pooling=True;Min Pool Size={MinPoolSize};Max Pool Size={MaxPoolSize}";
}
