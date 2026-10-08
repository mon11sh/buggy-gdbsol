using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionsService.Domain.Models;
using TransactionsService.DTOs;
using TransactionsService.Services;

namespace TransactionsService.Controllers;

/// <summary>
/// Exposes public REST endpoints for financial transactions (Deposits, Withdrawals, Transfers).
/// </summary>
/// <remarks>
/// Architectural Intent: Exposes endpoints for the Gateway. Strict Role-Based Access Control (RBAC) 
/// is enforced on all mutation endpoints (requires MANAGER or TELLER). Read endpoints are accessible 
/// to the account owner (validated via JWT claims).
/// </remarks>
[ApiController]
[Route("api/v1")]
public class TransactionsController : ControllerBase
{
    private readonly IDepositService _depositService;
    private readonly IWithdrawService _withdrawService;
    private readonly ITransferService _transferService;
    private readonly ITransactionLogService _transactionLogService;

    public TransactionsController(
        IDepositService depositService,
        IWithdrawService withdrawService,
        ITransferService transferService,
        ITransactionLogService transactionLogService)
    {
        _depositService = depositService;
        _withdrawService = withdrawService;
        _transferService = transferService;
        _transactionLogService = transactionLogService;
    }

    /// <summary>
    /// Initiates a deposit into a specified account.
    /// </summary>
    /// <remarks>
    /// Business Rule: The <c>Idempotency-Key</c> header is highly recommended to prevent accidental 
    /// double-deposits in the event of client retries or network timeouts.
    /// </remarks>
    [HttpPost("transactions/deposit")]
    [Authorize(Roles = "MANAGER,TELLER")]
    public async Task<ActionResult<TransactionResultResponse>> Deposit(
        [FromBody] DepositRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var result = await _depositService.ProcessDepositAsync(
            accountNumber: request.AccountNumber,
            amount: request.Amount,
            description: "Deposit",
            idempotencyKey: idempotencyKey
        , ct);
        return Created("", result);
    }

    [HttpPost("transactions/withdraw")]
    [Authorize(Roles = "MANAGER,TELLER")]
    public async Task<ActionResult<TransactionResultResponse>> Withdraw(
        [FromBody] WithdrawRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var result = await _withdrawService.ProcessWithdrawAsync(
            accountNumber: request.AccountNumber,
            amount: request.Amount,
            pin: request.Pin,
            description: "Withdrawal",
            idempotencyKey: idempotencyKey
        , ct);
        return Created("", result);
    }

    /// <summary>
    /// Initiates a fund transfer between two accounts.
    /// </summary>
    /// <remarks>
    /// Security Intent: Requires the source account's PIN.
    /// Architectural Intent: This acts as the entry point for a distributed Saga since it impacts 
    /// balances of two potentially disjoint domains.
    /// </remarks>
    [HttpPost("transactions/transfer")]
    [Authorize(Roles = "MANAGER,TELLER")]
    public async Task<ActionResult<TransferResultResponse>> Transfer(
        [FromBody] TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        TransferMode mode = TransferMode.NEFT;
        if (!string.IsNullOrEmpty(request.TransferMode))
        {
            if (!Enum.TryParse<TransferMode>(request.TransferMode, true, out var parsedMode) || !Enum.IsDefined(typeof(TransferMode), parsedMode))
            {
                mode = TransferMode.NEFT;
            }
            else
            {
                mode = parsedMode;
            }
        }

        var result = await _transferService.ProcessTransferAsync(
            fromAccount: request.FromAccount,
            toAccount: request.ToAccount,
            amount: request.Amount,
            pin: request.Pin,
            transferMode: mode,
            description: "Transfer",
            idempotencyKey: idempotencyKey
        , ct);
        return Created("", result);
    }


    [HttpGet("transactions")]
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

    /// <summary>One fund transfer with the two ledger legs it produced (source debit, destination credit).</summary>
    [HttpGet("transactions/transfers/{transfer_id:int}")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<TransferDetailsResponse>> GetTransfer([FromRoute(Name = "transfer_id")] int transfer_id, CancellationToken ct)
    {
        var transfer = await _transactionLogService.GetTransferDetailsAsync(transfer_id, ct);
        return transfer is null
            ? NotFound(new { error_code = "TRANSFER_NOT_FOUND", message = $"Transfer {transfer_id} not found" })
            : Ok(transfer);
    }

    [HttpGet("transactions/account/{account_number}")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")] // account history is staff-only, not "any valid JWT"
    public async Task<ActionResult<PagedTransactionResponse>> GetTransactionsByAccount(
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





