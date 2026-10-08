using Microsoft.AspNetCore.Mvc;

namespace RegistryService.Controllers;

public class HealthResponse
{
    public string status { get; set; } = "";
    public string service { get; set; } = "";
}

[ApiController]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    public ActionResult<HealthResponse> HealthCheck() => Ok(new HealthResponse { status = "healthy", service = "gdb-registry-service" });
}
