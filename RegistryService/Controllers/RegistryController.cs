using Gdb.Common.Security;
using Microsoft.AspNetCore.Mvc;
using RegistryService.DTOs;
using RegistryService.Services;

namespace RegistryService.Controllers;

[ApiController]
[InternalApi]
public class RegistryController : ControllerBase
{
    private readonly Services.ServiceRegistry _registry;

    public RegistryController(Services.ServiceRegistry registry)
    {
        _registry = registry;
    }

    [HttpPost("register")]
    public ActionResult Register([FromBody] RegistrationRequest request)
    {
        _registry.Register(request.Name, request.Url, request.Ttl);
        return Ok(new
        {
            status = "registered",
            name = request.Name,
            url = request.Url
        });
    }

    [HttpPost("heartbeat")]
    public ActionResult Heartbeat([FromBody] RegistrationRequest request)
    {
        if (!_registry.Heartbeat(request.Name, request.Url))
        {
            return NotFound(new { detail = "instance not registered" });
        }
        return Ok(new { status = "ok" });
    }

    [HttpDelete("register")]
    public ActionResult Deregister([FromBody] RegistrationRequest request)
    {
        _registry.Deregister(request.Name, request.Url);
        return Ok(new { status = "deregistered" });
    }

    [HttpGet("resolve/{name}")]
    public ActionResult Resolve(string name)
    {
        var url = _registry.Resolve(name);
        if (string.IsNullOrEmpty(url))
        {
            return NotFound(new { detail = $"no healthy instance for '{name}'" });
        }
        return Ok(new
        {
            name = name,
            url = url
        });
    }

    [HttpGet("services")]
    public ActionResult Services()
    {
        var names = _registry.Names();
        var result = new Dictionary<string, List<string>>();
        foreach (var name in names)
        {
            var healthyInstances = _registry.Healthy(name).Select(i => i.Url).ToList();
            result[name] = healthyInstances;
        }
        return Ok(result);
    }
}

