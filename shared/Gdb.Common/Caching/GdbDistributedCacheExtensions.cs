using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gdb.Common.Caching;

/// <summary>
/// One registration for the cross-instance state store (PIN lockouts, login throttles, cache
/// versions). With <c>Redis:ConnectionString</c> configured it is Redis, shared by every replica;
/// without it, it is an in-process store — fine for a single instance and the teaching stack,
/// and refused in Production because a second replica would silently get its own counters.
/// </summary>
public static class GdbDistributedCacheExtensions
{
    public const string ConnectionStringKey = "Redis:ConnectionString";

    public static IServiceCollection AddGdbDistributedCache(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, string instanceName)
    {
        var redis = configuration[ConnectionStringKey];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = instanceName + ":";
            });
            return services;
        }

        if (environment.IsProduction() && !configuration.GetValue<bool>("AllowSingleInstanceState"))
            throw new InvalidOperationException(
                $"Production requires a shared state store: set {ConnectionStringKey} (Redis), " +
                "or AllowSingleInstanceState=true to acknowledge that exactly one replica runs.");

        services.AddDistributedMemoryCache();
        return services;
    }
}
