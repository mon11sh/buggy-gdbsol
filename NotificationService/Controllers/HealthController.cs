using Microsoft.AspNetCore.Mvc;
using NotificationService.Config;

namespace NotificationService.Controllers;

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
            status = "active",
            service = _settings.AppName
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
