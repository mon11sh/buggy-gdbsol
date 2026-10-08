namespace Gdb.Common.Discovery;

public class StaticResolver
{
    private readonly Dictionary<string, string> _mapping;

    public StaticResolver(IDictionary<string, string> mapping)
    {
        _mapping = new Dictionary<string, string>(mapping, StringComparer.OrdinalIgnoreCase);
    }

    public string? Resolve(string name)
    {
        if (_mapping.TryGetValue(name, out var url))
        {
            return url;
        }
        return null;
    }
}
