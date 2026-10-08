using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gdb.Common.Middleware;

public class MetricsRegistry
{
    private readonly object _lock = new();
    private string _service = "unknown";
    
    private readonly Dictionary<(string Method, string Path, string Status), long> _reqTotal = new();
    private readonly Dictionary<(string Method, string Path), double> _durSum = new();
    private readonly Dictionary<(string Method, string Path), long> _durCount = new();
    private readonly Dictionary<(string Method, string Path, string Le), long> _durBucket = new();
    private int _inProgress = 0;

    private static readonly double[] Buckets = { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 };

    public void SetService(string service)
    {
        if (!string.IsNullOrEmpty(service))
        {
            _service = service;
        }
    }

    public void IncInProgress()
    {
        Interlocked.Increment(ref _inProgress);
    }

    public void DecInProgress()
    {
        Interlocked.Decrement(ref _inProgress);
    }

    public void Observe(string method, string path, int status, double duration)
    {
        var statusStr = status.ToString();
        
        lock (_lock)
        {
            var reqKey = (method, path, statusStr);
            _reqTotal[reqKey] = _reqTotal.TryGetValue(reqKey, out var c) ? c + 1 : 1;

            var durKey = (method, path);
            _durSum[durKey] = _durSum.TryGetValue(durKey, out var s) ? s + duration : duration;
            _durCount[durKey] = _durCount.TryGetValue(durKey, out var n) ? n + 1 : 1;

            foreach (var b in Buckets)
            {
                if (duration <= b)
                {
                    var bKey = (method, path, b.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _durBucket[bKey] = _durBucket.TryGetValue(bKey, out var bn) ? bn + 1 : 1;
                }
            }
            
            var infKey = (method, path, "+Inf");
            _durBucket[infKey] = _durBucket.TryGetValue(infKey, out var bnInf) ? bnInf + 1 : 1;
        }
    }

    private static string Escape(string v)
    {
        if (v == null) return "";
        return v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    public string Render()
    {
        Dictionary<(string Method, string Path, string Status), long> reqTotal;
        Dictionary<(string Method, string Path), double> durSum;
        Dictionary<(string Method, string Path), long> durCount;
        Dictionary<(string Method, string Path, string Le), long> durBucket;
        int inProgress;
        string service;

        lock (_lock)
        {
            reqTotal = new(_reqTotal);
            durSum = new(_durSum);
            durCount = new(_durCount);
            durBucket = new(_durBucket);
            inProgress = _inProgress;
            service = _service;
        }

        var sb = new StringBuilder();
        var svc = Escape(service);

        sb.AppendLine("# HELP http_requests_total Total HTTP requests processed.");
        sb.AppendLine("# TYPE http_requests_total counter");
        foreach (var kvp in reqTotal.OrderBy(x => x.Key.Method).ThenBy(x => x.Key.Path).ThenBy(x => x.Key.Status))
        {
            sb.AppendLine($"http_requests_total{{service=\"{svc}\",method=\"{Escape(kvp.Key.Method)}\",path=\"{Escape(kvp.Key.Path)}\",status=\"{Escape(kvp.Key.Status)}\"}} {kvp.Value}");
        }

        sb.AppendLine("# HELP http_request_duration_seconds HTTP request latency in seconds.");
        sb.AppendLine("# TYPE http_request_duration_seconds histogram");
        foreach (var kvp in durCount.OrderBy(x => x.Key.Method).ThenBy(x => x.Key.Path))
        {
            var m = Escape(kvp.Key.Method);
            var p = Escape(kvp.Key.Path);
            var bBase = $"service=\"{svc}\",method=\"{m}\",path=\"{p}\"";

            foreach (var b in Buckets)
            {
                var bStr = b.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var key = (kvp.Key.Method, kvp.Key.Path, bStr);
                var count = durBucket.TryGetValue(key, out var val) ? val : 0;
                sb.AppendLine($"http_request_duration_seconds_bucket{{{bBase},le=\"{bStr}\"}} {count}");
            }
            
            var infKey = (kvp.Key.Method, kvp.Key.Path, "+Inf");
            var infCount = durBucket.TryGetValue(infKey, out var infVal) ? infVal : 0;
            sb.AppendLine($"http_request_duration_seconds_bucket{{{bBase},le=\"+Inf\"}} {infCount}");
            
            var sum = durSum.TryGetValue(kvp.Key, out var s) ? s : 0.0;
            sb.AppendLine($"http_request_duration_seconds_sum{{{bBase}}} {sum.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"http_request_duration_seconds_count{{{bBase}}} {kvp.Value}");
        }

        sb.AppendLine("# HELP http_requests_in_progress In-flight HTTP requests.");
        sb.AppendLine("# TYPE http_requests_in_progress gauge");
        sb.AppendLine($"http_requests_in_progress{{service=\"{svc}\"}} {inProgress}");

        return sb.ToString();
    }
}

public class MetricsMiddleware : IMiddleware
{
    private readonly MetricsRegistry _registry;
    private readonly string _path;

    public MetricsMiddleware(MetricsRegistry registry, string path = "/metrics", string? service = null)
    {
        _registry = registry;
        _path = path;
        if (service != null) _registry.SetService(service);
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path == _path)
        {
            await next(context);
            return;
        }

        var sw = ValueStopwatch.StartNew();
        _registry.IncInProgress();
        int status = 500;

        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        finally
        {
            _registry.DecInProgress();
            var duration = sw.GetElapsedTime().TotalSeconds;
            
            var endpoint = context.GetEndpoint();
            var routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? "unknown";
            
            _registry.Observe(context.Request.Method, routePattern, status, duration);
        }
    }
}

internal struct ValueStopwatch
{
    private readonly long _startTimestamp;
    private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;
    public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());
    public TimeSpan GetElapsedTime() => Stopwatch.GetElapsedTime(_startTimestamp);
}
