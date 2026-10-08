using AccountsService.DTOs;
using AccountsService.Mapping;
using AccountsService.Services;
using AccountsService.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountsService.Controllers;

[ApiController]
[Route("api/v1/accounts")]
/// <summary>
/// Exposes public REST endpoints for the Accounts domain.
/// </summary>
/// <remarks>
/// Architectural Intent: This controller is exclusively for front-end consumption via the Gateway. 
/// It relies on Role-Based Access Control (RBAC) extracting roles from the JWT claims. 
/// Service-to-service calls MUST use <c>InternalAccountController</c> instead.
/// </remarks>
public class AccountController : ControllerBase
{
    private readonly IAccountService _useCases;
    private readonly AccountResponseMapper _mapper;

    public AccountController(IAccountService useCases, AccountResponseMapper mapper)
    {
        _useCases = useCases;
        _mapper = mapper;
    }

    /// <summary>
    /// Opens a new Savings account and validates the applicant's Aadhar number.
    /// </summary>
    /// <remarks>
    /// Business Rule: Aadhar verification is strictly required for individual accounts.
    /// </remarks>
    [HttpPost("savings")]
    [Authorize(Roles = "ADMIN,TELLER")]
    public async Task<ActionResult<AccountResponse>> CreateSavingsAccount([FromBody] SavingsAccountCreate request, CancellationToken ct)
    {
        var account = await _useCases.CreateSavingsAccountAsync(request, ct);
        return Created($"/api/v1/accounts/{account.AccountNumber}", _mapper.ToResponse(account));
    }

    [HttpPost("current")]
    [Authorize(Roles = "ADMIN,TELLER")]
    public async Task<ActionResult<AccountResponse>> CreateCurrentAccount([FromBody] CurrentAccountCreate request, CancellationToken ct)
    {
        var account = await _useCases.CreateCurrentAccountAsync(request, ct);
        return Created($"/api/v1/accounts/{account.AccountNumber}", _mapper.ToResponse(account));
    }

    [HttpGet]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<List<AccountResponse>>> GetAllAccounts([FromQuery] string? account_type, [FromQuery] int skip = 0, [FromQuery] int? limit = null, CancellationToken ct = default)
    {
        if (account_type != null && account_type != "SAVINGS" && account_type != "CURRENT")
            return BadRequest(new { error_code = "INVALID_ACCOUNT_TYPE", message = "account_type must be either 'SAVINGS' or 'CURRENT'" });

        (skip, limit) = Gdb.Common.Http.Paging.Clamp(skip, limit);
        var accounts = await _useCases.ListAccountsAsync(account_type, ct);
        Response.Headers[Gdb.Common.Http.Paging.TotalCountHeader] = accounts.Count.ToString();

        // ponytail: paging over the (cached) full list; push Skip/Take into the repository when the
        // table outgrows what the AccountListCache can hold.
        var query = accounts.Skip(skip).Take(limit.Value);

        // Project to `object` (not the base AccountResponse) so System.Text.Json serializes each
        // element by its RUNTIME type. Serializing a List<AccountResponse> would use the declared
        // base type and silently drop the Savings/Current-specific fields (date_of_birth, gender,
        // phone_no, aadhar_number, company_name, ...), leaving them blank on the client.
        var result = query
            .Select(a => (object)_mapper.ToResponse(a))
            .ToList();

        return Ok(result);
    }

    /// <summary>Staff search by holder name and/or privilege tier (raw-SQL backed; see AccountRepository.SearchAsync).</summary>
    [HttpGet("search")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<List<AccountResponse>>> SearchAccounts([FromQuery] string? q, [FromQuery] string? privilege, [FromQuery] int? limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(privilege))
            return BadRequest(new { error_code = "VALIDATION_ERROR", message = "Provide q (name contains) and/or privilege" });

        var (_, take) = Gdb.Common.Http.Paging.Clamp(0, limit ?? 50);
        var accounts = await _useCases.SearchAccountsAsync(q, privilege, take, ct);
        return Ok(accounts.Select(a => (object)_mapper.ToResponse(a)).ToList());
    }

    [HttpGet("summary")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<AccountSummaryResponse>> GetAccountsSummary(CancellationToken ct)
    {
        var summary = await _useCases.GetAccountSummaryAsync(ct: ct);
        return Ok(summary);
    }

    [HttpGet("{account_number}")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<AccountResponse>> GetAccount(int account_number, CancellationToken ct)
    {
        var account = await _useCases.GetAccountAsync(account_number, ct);
        return _mapper.ToResponse(account!);
    }

    [HttpGet("{account_number}/balance")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<BalanceResponse>> GetBalance(int account_number, CancellationToken ct)
    {
        var balance = await _useCases.GetBalanceAsync(account_number, ct);
        return new BalanceResponse { AccountNumber = account_number, Balance = balance, Currency = "INR" };
    }

    [HttpPut("{account_number}")]
    [Authorize(Roles = "ADMIN,TELLER")]
    public async Task<ActionResult<AccountActionResponse>> UpdateAccount(int account_number, [FromBody] AccountUpdate request, CancellationToken ct)
    {
        // The privilege tier raises transfer limits: a TELLER may edit contact details but not re-tier an account.
        if (request.Privilege is not null && !User.IsInRole("ADMIN"))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error_code = "FORBIDDEN", message = "Only ADMIN may change an account's privilege tier" });

        await _useCases.UpdateAccountAsync(account_number, request, ct);
        return Ok(new AccountActionResponse { Success = true, Message = "Account updated successfully", AccountNumber = account_number });
    }

    [HttpPost("{account_number}/activate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<AccountActionResponse>> ActivateAccount(int account_number, CancellationToken ct)
    {
        var success = await _useCases.ActivateAccountAsync(account_number, ct);
        return Ok(new AccountActionResponse { Success = success, Message = "Account activated successfully", AccountNumber = account_number });
    }

    [HttpPost("{account_number}/inactivate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<AccountActionResponse>> InactivateAccount(int account_number, CancellationToken ct)
    {
        var success = await _useCases.InactivateAccountAsync(account_number, ct);
        return Ok(new AccountActionResponse { Success = success, Message = "Account inactivated successfully", AccountNumber = account_number });
    }

    [HttpPost("{account_number}/close")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<AccountActionResponse>> CloseAccount(int account_number, CancellationToken ct)
    {
        var success = await _useCases.CloseAccountAsync(account_number, ct);
        return Ok(new AccountActionResponse { Success = success, Message = "Account closed successfully", AccountNumber = account_number });
    }

    /// <summary>
    /// Verifies the account PIN against the securely hashed stored value.
    /// </summary>
    /// <remarks>
    /// Security Intent: Protects against brute-force attacks via <c>IPinLockoutService</c> 
    /// which will lock the account after consecutive failures.
    /// </remarks>
    [HttpPost("{account_number}/verify-pin")]
    [Authorize(Roles = "ADMIN,TELLER,MANAGER")]
    public async Task<ActionResult<PinVerifyResponse>> VerifyPin(int account_number, [FromBody] PinVerifyRequest request, CancellationToken ct)
    {
        // Lockout is enforced inside the service so the public and internal PIN paths share one counter;
        // a locked account surfaces as AccountLockedError -> 423 via the exception middleware.
        try
        {
            await _useCases.VerifyPinAsync(account_number, request.Pin, ct);
            return Ok(new PinVerifyResponse { Valid = true, Message = "PIN verified successfully" });
        }
        catch (Domain.Exceptions.InvalidPinError)
        {
            return Unauthorized(new { error_code = "INVALID_PIN", message = "Invalid PIN" });
        }
    }
}

