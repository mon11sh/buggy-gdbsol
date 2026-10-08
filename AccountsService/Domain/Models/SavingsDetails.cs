namespace AccountsService.Domain.Models;

public class SavingsDetails
{
    public string DateOfBirth { get; private set; }
    public string Gender { get; private set; }
    public string PhoneNumber { get; private set; }
    public string Aadhaar { get; private set; }
    public string AadhaarHash { get; private set; } // Kept for indexing/persistence compatibility

    public SavingsDetails(string dateOfBirth, string gender, string phoneNumber, string aadhaar, string aadhaarHash)
    {
        DateOfBirth = dateOfBirth;
        Gender = gender;
        PhoneNumber = phoneNumber;
        Aadhaar = aadhaar;
        AadhaarHash = aadhaarHash;
    }
}
