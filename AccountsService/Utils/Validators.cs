using AccountsService.Domain.Exceptions;

namespace AccountsService.Utils;

public static class Validators
{
    public static void ValidatePin(string pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length < 4 || pin.Length > 6)
            throw new InvalidPinError("PIN must be between 4 and 6 digits");
            
        if (!pin.All(char.IsDigit))
            throw new InvalidPinError("PIN must contain only digits");
            
        if (pin.Distinct().Count() == 1)
            throw new InvalidPinError("PIN cannot have all identical digits");
            
        // Check for purely sequential
        var digits = pin.Select(c => c - '0').ToArray();
        bool isAscending = true;
        bool isDescending = true;
        
        for (int i = 0; i < digits.Length - 1; i++)
        {
            if (digits[i + 1] - digits[i] != 1) isAscending = false;
            if (digits[i] - digits[i + 1] != 1) isDescending = false;
        }
        
        if (isAscending || isDescending)
            throw new InvalidPinError("PIN cannot be purely sequential (like 1234 or 4321)");
    }
}
