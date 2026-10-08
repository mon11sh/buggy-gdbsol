namespace Gdb.Common.Discovery;

public class ServiceInstance
{
    public string Name { get; set; }
    public string Url { get; set; }
    public long RegisteredAt { get; set; }
    public double Ttl { get; set; }

    public ServiceInstance(string name, string url, long registeredAt, double ttl)
    {
        Name = name;
        Url = url;
        RegisteredAt = registeredAt;
        Ttl = ttl;
    }

    public bool IsAlive(long now)
    {
        // TTL is in seconds, TickCount64 is in milliseconds
        var elapsedSeconds = (now - RegisteredAt) / 1000.0;
        return elapsedSeconds <= Ttl;
    }
}
