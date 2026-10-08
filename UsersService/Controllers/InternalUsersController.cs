using Gdb.Common.Security;
using Microsoft.AspNetCore.Mvc;
using UsersService.Services;


namespace UsersService.Controllers;

[ApiController]
[Route("internal/v1/users")]
[InternalApi]
[Microsoft.AspNetCore.Authorization.AllowAnonymous] // service-to-service: guarded by the internal API key above, not by a user JWT
public class InternalUsersController : ControllerBase
{
    private readonly UserService _userService;

    public InternalUsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyCredentials([FromBody] VerifyCredentialsRequest request, CancellationToken ct)
    {
        var result = await _userService.VerifyUserCredentialsAsync(request.LoginId, request.Password, ct);
        return Ok(result);
    }

    [HttpGet("{login_id}/status")]
    public async Task<IActionResult> GetUserStatus(string login_id, CancellationToken ct)
    {
        var result = await _userService.GetUserStatusAsync(login_id, ct);
        if (result == null) return NotFound(new { error_code = "USER_NOT_FOUND", detail = "User not found" });
        return Ok(result);
    }

    [HttpGet("{login_id}/role")]
    public async Task<IActionResult> GetUserRole(string login_id, CancellationToken ct)
    {
        var result = await _userService.GetUserRoleAsync(login_id, ct);
        if (result == null) return NotFound(new { error_code = "USER_NOT_FOUND", detail = "User not found" });
        return Ok(result);
    }

    [HttpPost("validate-role")]
    public async Task<IActionResult> ValidateUserRole([FromBody] ValidateRoleRequest request, CancellationToken ct)
    {
        var result = await _userService.ValidateUserRoleAsync(request.LoginId, request.RequiredRole, ct);
        if (result == null) return NotFound(new { error_code = "USER_NOT_FOUND", detail = "User not found" });
        return Ok(result);
    }

    [HttpPost("bulk-validate")]
    public async Task<IActionResult> BulkValidateUsers([FromBody] BulkValidateRequest request, CancellationToken ct)
    {
        if (request.LoginIds == null || request.LoginIds.Count == 0)
            return BadRequest(new { error_code = "INVALID_REQUEST", detail = "login_ids cannot be empty" });
            
        var result = await _userService.BulkValidateUsersAsync(request.LoginIds, ct);
        return Ok(result);
    }

    [HttpGet("/internal/v1/health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            service = "User Management Service - Internal APIs",
            version = "1.0.0"
        });
    }
}

public class VerifyCredentialsRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("login_id")]
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    public string LoginId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("password")]
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public class ValidateRoleRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("login_id")]
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    public string LoginId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("required_role")]
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(ADMIN|TELLER|MANAGER)$")]
    public string RequiredRole { get; set; } = string.Empty;
}

public class BulkValidateRequest
{
    // Bounded: each id costs a lookup, so an unbounded list is a trivial resource-exhaustion vector.
    [System.Text.Json.Serialization.JsonPropertyName("login_ids")]
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(1)]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public System.Collections.Generic.List<string> LoginIds { get; set; } = new();
}

