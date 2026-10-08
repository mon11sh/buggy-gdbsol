using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionsService.DTOs;
using TransactionsService.Services;

namespace TransactionsService.Controllers;

[ApiController]
[Route("api/v1/transfer-limits")]
public class TransferLimitController : ControllerBase
{
    private readonly TransferLimitService _transferLimitService;

    public TransferLimitController(TransferLimitService transferLimitService)
    {
        _transferLimitService = transferLimitService;
    }

    [HttpGet("rules/all")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<IActionResult> GetAllLimits(CancellationToken ct)
    {
        var limits = await _transferLimitService.GetAllLimitsAsync(ct);
        var result = limits.Select(l => new {
            privilege = l.Privilege,
            daily_limit = (double)l.DailyLimit,
            transaction_limit = (int)l.PerTransactionLimit,
            allowed_modes = l.AllowedModes.Select(m => m.Mode).OrderBy(m => m).ToList(),
            created_at = ""
        });
        return Ok(result);
    }

    [HttpGet("{accountNumber}")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<IActionResult> GetTransferLimit(int accountNumber, CancellationToken ct)
    {
        try
        {
            var limit = await _transferLimitService.GetTransferLimitAsync(accountNumber, ct);
            return Ok(limit);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpGet("remaining/{accountNumber}")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<IActionResult> GetRemainingLimit(int accountNumber, CancellationToken ct)
    {
        try
        {
            var limit = await _transferLimitService.GetRemainingLimitAsync(accountNumber, ct);
            return Ok(limit);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpPost("check")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<IActionResult> CheckCanTransfer([FromQuery(Name="account_number")] int account_number, [FromQuery(Name="amount")] decimal amount, CancellationToken ct)
    {
        try
        {
            var result = await _transferLimitService.CheckCanTransferAsync(account_number, amount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpPut("rules/{privilege}")]
    [Authorize(Roles = "ADMIN,TELLER")]
    public async Task<IActionResult> UpdateLimit(string privilege, [FromBody] TransferLimitUpdate request, CancellationToken ct)
    {
        var limit = await _transferLimitService.UpdateLimitAsync(privilege, request.DailyLimit, request.TransactionLimit, ct);
        if (limit == null)
            return NotFound(new { error_code = "LIMIT_NOT_FOUND", message = "Transfer limit not found", status = "error" });
            
        return Ok(new {
            privilege = limit.Privilege,
            daily_limit = (double)limit.DailyLimit,
            transaction_limit = (int)limit.PerTransactionLimit
        });
    }
}


