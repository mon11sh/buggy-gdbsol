using Gdb.Common.Security;
using AccountsService.DTOs;

using AccountsService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccountsService.Controllers;

[ApiController]
[Route("api/v1/internal/accounts")]
[InternalApi] // Protect all endpoints in this controller
[Microsoft.AspNetCore.Authorization.AllowAnonymous] // service-to-service: guarded by the internal API key above, not by a user JWT
/// <summary>
/// Exposes internal endpoints for cross-microservice communication (e.g. TransactionsService needing to debit/credit).
/// </summary>
/// <remarks>
/// Architectural Intent: Bypasses user-level RBAC and relies strictly on the <c>[InternalApi]</c> 
/// API key mechanism, meaning it cannot be reached directly from the public Gateway.
/// </remarks>
public class InternalAccountController : ControllerBase
{
    private readonly IAccountInternalService _useCases;

    public InternalAccountController(IAccountInternalService useCases)
    {
        _useCases = useCases;
    }

    [HttpGet("{account_number}")]
    public async Task<ActionResult<InternalAccountDetailsResponse>> GetAccountDetails(int account_number, CancellationToken ct)
    {
        return Ok(await _useCases.GetAccountDetailsInternalAsync(account_number, ct));
    }

    [HttpGet("{account_number}/privilege")]
    public async Task<ActionResult<InternalPrivilegeResponse>> GetPrivilege(int account_number, CancellationToken ct)
    {
        var result = await _useCases.GetPrivilegeInternalAsync(account_number, ct);
        if (result.Status == "FAILED")
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{account_number}/active")]
    public async Task<ActionResult<InternalActiveResponse>> CheckActive(int account_number, CancellationToken ct)
    {
        var result = await _useCases.CheckActiveInternalAsync(account_number, ct);
        if (result.Status == "FAILED")
            return NotFound(result);
        return Ok(result);
    }

    // Secrets and amounts travel in the JSON body, never in the query string (proxy/access logs, history).
    [HttpPost("{account_number}/verify-pin")]
    public async Task<ActionResult<InternalPinVerifyResponse>> VerifyPin(int account_number, [FromBody] PinVerifyRequest request, CancellationToken ct)
    {
        var result = await _useCases.VerifyPinInternalAsync(account_number, request.Pin, ct);
        if (result.Status == "FAILED")
        {
            if (result.ErrorCode == "ACCOUNT_NOT_FOUND")
                return NotFound(result);
            return Unauthorized(result);
        }
        return Ok(result);
    }

    [HttpPost("{account_number}/debit")]
    public async Task<ActionResult<InternalTransactionResponse>> Debit(int account_number, [FromBody] DebitRequest request, CancellationToken ct)
    {
        var result = await _useCases.DebitAccountInternalAsync(account_number, request.Amount, ct);
        if (result.Status == "FAILED")
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{account_number}/credit")]
    public async Task<ActionResult<InternalTransactionResponse>> Credit(int account_number, [FromBody] CreditRequest request, CancellationToken ct)
    {
        var result = await _useCases.CreditAccountInternalAsync(account_number, request.Amount, ct);
        if (result.Status == "FAILED")
            return BadRequest(result);
        return Ok(result);
    }
}

