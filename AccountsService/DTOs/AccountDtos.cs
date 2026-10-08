using System.ComponentModel.DataAnnotations;
using AccountsService.Utils;

namespace AccountsService.DTOs;

public record AccountBase
{
    [Required]
    [StringLength(255, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(SILVER|GOLD|PREMIUM)$", ErrorMessage = "Privilege must be SILVER, GOLD or PREMIUM")]
    public string Privilege { get; init; } = "SILVER";

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string BankName { get; init; } = "Global Digital Bank";

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string BankBranch { get; init; } = "Main Branch";

    // Project convention is the 10-char demo code GDB0000001 (real IFSCs are 11): accept both, uppercase alphanumeric only.
    [Required]
    [RegularExpression("^[A-Z0-9]{10,11}$", ErrorMessage = "IFSC must be 10-11 uppercase letters/digits")]
    public string IfscCode { get; init; } = "GDB0000001";
}

public record SavingsAccountCreate : AccountBase
{
    [Required]
    [StringLength(6, MinimumLength = 4)]  // 4-6 digits, consistent with ValidatePin + PinVerifyRequest
    public string Pin { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Invalid date format. Use YYYY-MM-DD")]
    public string DateOfBirth { get; init; } = string.Empty;

    [Required]
    public string Gender { get; init; } = string.Empty;

    [Required]
    [StringLength(10, MinimumLength = 10)]
    [RegularExpression(@"^\d+$", ErrorMessage = "Phone number must be numeric")]
    public string PhoneNo { get; init; } = string.Empty;

    [Required]
    [StringLength(12, MinimumLength = 12)]
    [RegularExpression(@"^\d+$", ErrorMessage = "Aadhar number must contain only digits")]
    public string AadharNumber { get; init; } = string.Empty;

    public string AccountType { get; init; } = "SAVINGS";

    // Opening deposit: floor is the savings minimum, ceiling stops an operator minting an arbitrary balance.
    [Range(2000.0, 10000000.0, ErrorMessage = "Initial balance must be between 2,000 and 1,00,00,000")]
    public decimal InitialBalance { get; init; } = 2000.0m;
}

public record CurrentAccountCreate : AccountBase
{
    [Required]
    [StringLength(6, MinimumLength = 4)]  // 4-6 digits, consistent with ValidatePin + PinVerifyRequest
    public string Pin { get; init; } = string.Empty;

    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string CompanyName { get; init; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string RegistrationNo { get; init; } = string.Empty;

    public string AccountType { get; init; } = "CURRENT";

    [MaxLength(255)]
    public string? Website { get; init; }
}

public record AccountResponse : AccountBase
{
    public int AccountNumber { get; init; }
    public string AccountType { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public bool IsActive { get; init; }
    public DateTime ActivatedDate { get; init; }
    public DateTime? ClosedDate { get; init; }

    // Polymorphic, per-account-type figures resolved from the domain model:
    // MinimumBalance comes from the abstract Account.GetMinimumBalance();
    // MonthlyMaintenanceFee from the virtual Account.GetMonthlyMaintenanceFee().
    public decimal MinimumBalance { get; init; }
    public decimal MonthlyMaintenanceFee { get; init; }
}

public record SavingsAccountResponse : AccountResponse
{
    public string DateOfBirth { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string PhoneNo { get; init; } = string.Empty;

    private string _aadharNumber = string.Empty;
    public string AadharNumber
    {
        get => _aadharNumber;
        init
        {
            _aadharNumber = (value.Length == 12) ? new string('*', 8) + value.Substring(8) : value;
        }
    }
}

public record CurrentAccountResponse : AccountResponse
{
    public string CompanyName { get; init; } = string.Empty;
    public string RegistrationNo { get; init; } = string.Empty;
    public string? Website { get; init; }
}

public record BalanceResponse
{
    public int AccountNumber { get; init; }
    public decimal Balance { get; init; }
    public string Currency { get; init; } = "INR";
}

public record DebitRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }
    public string? Description { get; init; }
    public string? IdempotencyKey { get; init; }
}

public record CreditRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }
    public string? Description { get; init; }
    public string? IdempotencyKey { get; init; }
}

public record PinVerifyRequest
{
    [Required]
    [StringLength(6, MinimumLength = 4)]
    public string Pin { get; init; } = string.Empty;
}

public record AccountDetailsResponse
{
    public int AccountNumber { get; init; }
    public string AccountType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public string Privilege { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime ActivatedDate { get; init; }
    public string BankName { get; init; } = string.Empty;
    public string BankBranch { get; init; } = string.Empty;
    public string IfscCode { get; init; } = string.Empty;
    public DateTime? ClosedDate { get; init; }
}

public record AccountUpdate
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; init; }

    // Tier drives transfer limits: constrained to the known tiers and (see controller) ADMIN-only to change.
    [RegularExpression("^(SILVER|GOLD|PREMIUM)$", ErrorMessage = "Privilege must be SILVER, GOLD or PREMIUM")]
    public string? Privilege { get; init; }
    [StringLength(20, MinimumLength = 10)]
    [RegularExpression(@"^\d+$", ErrorMessage = "Phone number must be numeric")]
    public string? PhoneNo { get; init; }
    [StringLength(255, MinimumLength = 1)]
    public string? CompanyName { get; init; }
    [MaxLength(255)]
    public string? Website { get; init; }
}
