using Microsoft.AspNetCore.Mvc;

namespace AccountsService.Controllers;
// Branch 1 change
// Branch 2 change
[ApiController]
[Route("api/v1")]
[ApiExplorerSettings(IgnoreApi = true)]
[Microsoft.AspNetCore.Authorization.AllowAnonymous] // liveness probe: must answer without a token
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public ActionResult HealthCheck() => Ok(new { status = "healthy", service = "gdb-accounts-service" });
}
//merge demo
//cherry pick
