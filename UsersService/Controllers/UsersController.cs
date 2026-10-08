using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsersService.DTOs;
using UsersService.Services;

namespace UsersService.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> AddUser([FromBody] AddUserRequest request, CancellationToken ct)
    {
        var performedBy = User.FindFirst("login_id")?.Value;
        var response = await _userService.AddUserAsync(request, performedBy, ct);
        return Created("", response);
    }

    [HttpPut("{login_id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> EditUser(string login_id, [FromBody] EditUserRequest request, CancellationToken ct)
    {
        var performedBy = User.FindFirst("login_id")?.Value;
        var response = await _userService.EditUserAsync(login_id, request, performedBy, ct);
        return Ok(response);
    }

    [HttpGet]
    [Authorize(Roles = "TELLER,ADMIN")]
    public async Task<IActionResult> ListUsers([FromQuery] int skip = 0, [FromQuery] int? limit = null, CancellationToken ct = default)
    {
        (skip, limit) = Gdb.Common.Http.Paging.Clamp(skip, limit);
        var response = await _userService.GetAllUsersAsync(skip, limit.Value, ct);
        return Ok(response);
    }

    [HttpGet("{login_id}")]
    [Authorize(Roles = "MANAGER,TELLER,ADMIN")]
    public async Task<IActionResult> ViewUser(string login_id, CancellationToken ct)
    {
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var requestingLoginId = User.FindFirst("login_id")?.Value;
        
        if (userRole != "ADMIN" && requestingLoginId != login_id)
        {
            return StatusCode(403, new { error_code = "FORBIDDEN", detail = "You can only view your own profile" });
        }

        var response = await _userService.GetUserAsync(login_id, ct);
        return Ok(response);
    }

    [HttpPatch("{login_id}/inactivate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> InactivateUser(string login_id, CancellationToken ct)
    {
        var performedBy = User.FindFirst("login_id")?.Value;
        var response = await _userService.InactivateUserAsync(login_id, performedBy, ct);
        return Ok(response);
    }

    [HttpPatch("{login_id}/activate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ActivateUser(string login_id, CancellationToken ct)
    {
        var performedBy = User.FindFirst("login_id")?.Value;
        var response = await _userService.ActivateUserAsync(login_id, performedBy, ct);
        return Ok(response);
    }
}
