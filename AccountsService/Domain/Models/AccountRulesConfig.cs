namespace AccountsService.Domain.Models;

public static class AccountRulesConfig
{
    public const int MIN_SAVINGS_AGE = 18;
    public const decimal MIN_SAVINGS_INITIAL_BALANCE = 2000.0m;
    public const decimal CURRENT_OPENING_BALANCE = 0.0m;

    // Regulatory minimum balances each account type must keep (see Account.GetMinimumBalance).
    public const decimal SAVINGS_MIN_BALANCE = 1000.0m;
    public const decimal CURRENT_MIN_BALANCE = 5000.0m;

    // Monthly account-maintenance fee (see Account.GetMonthlyMaintenanceFee).
    public const decimal SAVINGS_MONTHLY_FEE = 0.0m;
    public const decimal CURRENT_MONTHLY_FEE = 500.0m;
}
