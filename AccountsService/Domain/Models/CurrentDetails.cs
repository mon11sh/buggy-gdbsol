namespace AccountsService.Domain.Models;

public class CurrentDetails
{
    public string AccountHolderName { get; private set; }
    public string RegistrationNumber { get; private set; }
    public string Website { get; private set; }

    public CurrentDetails(string accountHolderName, string registrationNumber, string? website)
    {
        AccountHolderName = accountHolderName;
        RegistrationNumber = registrationNumber;
        Website = website ?? string.Empty;
    }
}
