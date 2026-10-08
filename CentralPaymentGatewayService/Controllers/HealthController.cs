using Microsoft.AspNetCore.Mvc;

namespace CentralPaymentGatewayService.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("live")]
    public ActionResult LiveCheck() => Ok(new { status = "alive" });

    [HttpGet("ready")]
    public ActionResult ReadyCheck() => Ok(new { status = "ready" });
}
