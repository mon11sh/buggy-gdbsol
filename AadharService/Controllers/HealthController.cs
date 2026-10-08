using Microsoft.AspNetCore.Mvc;
using AadharService.Config;

namespace AadharService.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly Settings _settings;

    public HealthController(Settings settings)
    {
        _settings = settings;
    }

    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            service = _settings.AppName,
            status = "healthy",
            version = "1.0.0"
        });
    }

    [HttpGet("live")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Live()
    {
        return Ok(new { status = "alive" });
    }

    [HttpGet("ready")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Ready()
    {
        return Ok(new { status = "ready" });
    }
}
