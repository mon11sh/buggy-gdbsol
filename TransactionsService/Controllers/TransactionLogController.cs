using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionsService.Services;
using TransactionsService.DTOs;

namespace TransactionsService.Controllers;

[ApiController]
[Route("api/v1/transaction-logs")]
public class TransactionLogController : ControllerBase
{
    private readonly ITransactionLogService _transactionLogService;

    public TransactionLogController(ITransactionLogService transactionLogService)
    {
        _transactionLogService = transactionLogService;
    }

    [HttpGet]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<PagedTransactionResponse>> GetAllTransactions(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string? type = null,
        [FromQuery] string? start_date = null,
        [FromQuery] string? end_date = null, CancellationToken ct = default)
    {
        (skip, limit) = Gdb.Common.Http.Paging.Clamp(skip, limit);
        DateTime? startDateParsed = null;
        DateTime? endDateParsed = null;

        if (!string.IsNullOrEmpty(start_date) && DateTime.TryParse(start_date, out var sd)) startDateParsed = sd;
        if (!string.IsNullOrEmpty(end_date) && DateTime.TryParse(end_date, out var ed)) endDateParsed = ed;

        var result = await _transactionLogService.GetAllTransactionsAsync(
            skip, limit, type, startDateParsed, endDateParsed, ct);

        return Ok(result);
    }

    [HttpGet("{account_number}")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")] // account history is staff-only, not "any valid JWT"
    public async Task<ActionResult<PagedTransactionResponse>> GetAccountTransactions(
        [FromRoute(Name="account_number")] int account_number,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string? type = null,
        [FromQuery] string? start_date = null,
        [FromQuery] string? end_date = null, CancellationToken ct = default)
    {
        (skip, limit) = Gdb.Common.Http.Paging.Clamp(skip, limit);
        DateTime? startDateParsed = null;
        DateTime? endDateParsed = null;

        if (!string.IsNullOrEmpty(start_date) && DateTime.TryParse(start_date, out var sd)) startDateParsed = sd;
        if (!string.IsNullOrEmpty(end_date) && DateTime.TryParse(end_date, out var ed)) endDateParsed = ed;

        var result = await _transactionLogService.GetTransactionLogsAsync(
            account_number, skip, limit, startDateParsed, endDateParsed, type, ct);

        return Ok(result);
    }
}





