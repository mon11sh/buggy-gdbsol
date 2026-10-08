using System.Collections.Concurrent;

namespace Gdb.Common.Discovery;

public class ServiceRegistry
{
    private readonly ConcurrentDictionary<string, List<ServiceInstance>> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _rr = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    protected virtual long GetNow() => Environment.TickCount64;

    public void Register(string name, string url, double ttl = 30.0)
    {
        lock (_lock)
        {
            var instances = _services.GetOrAdd(name, _ => new List<ServiceInstance>());
            foreach (var inst in instances)
            {
                if (inst.Url.Equals(url, StringComparison.OrdinalIgnoreCase))
                {
                    inst.RegisteredAt = GetNow();
                    inst.Ttl = ttl;
                    return;
                }
            }
            instances.Add(new ServiceInstance(name, url, GetNow(), ttl));
        }
    }

    public bool Heartbeat(string name, string url)
    {
        lock (_lock)
        {
            if (_services.TryGetValue(name, out var instances))
            {
                foreach (var inst in instances)
                {
                    if (inst.Url.Equals(url, StringComparison.OrdinalIgnoreCase))
                    {
                        inst.RegisteredAt = GetNow();
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public void Deregister(string name, string url)
    {
        lock (_lock)
        {
            if (_services.TryGetValue(name, out var instances))
            {
                instances.RemoveAll(i => i.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public List<ServiceInstance> Healthy(string name)
    {
        var now = GetNow();
        lock (_lock)
        {
            if (_services.TryGetValue(name, out var instances))
            {
                return instances.Where(i => i.IsAlive(now)).ToList();
            }
        }
        return new List<ServiceInstance>();
    }

    public string? Resolve(string name)
    {
        var instances = Healthy(name);
        if (instances.Count == 0) return null;

        lock (_lock)
        {
            var idx = _rr.GetOrAdd(name, 0) % instances.Count;
            _rr[name] = idx + 1;
            return instances[idx].Url;
        }
    }

    public List<string> Names()
    {
        var now = GetNow();
        var names = new List<string>();
        lock (_lock)
        {
            foreach (var kvp in _services)
            {
                if (kvp.Value.Any(i => i.IsAlive(now)))
                {
                    names.Add(kvp.Key);
                }
            }
        }
        return names;
    }
}
