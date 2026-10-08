using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public ActionResult HealthCheck() => Ok(new { status = "healthy", service = "gdb-auth-service" });
}
