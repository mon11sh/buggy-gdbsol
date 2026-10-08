using Microsoft.AspNetCore.Mvc;

namespace CentralGatewayService.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            service = "api-gateway"
        });
    }
}
