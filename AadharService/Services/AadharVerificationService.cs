using AadharService.DTOs;

namespace AadharService.Services;

public class AadharVerificationService
{
    private readonly ILogger<AadharVerificationService> _logger;

    private static readonly Dictionary<string, AadharHolderData> AadharDatabase = new()
{
    { "123456789012", new AadharHolderData { Name = "Rajesh Kumar", MobileNo = "9876543210", Address = "123, MG Road, Bangalore, Karnataka - 560001", Gender = "Male", DateOfBirth = "1990-05-15", PhotoUrl = "https://randomuser.me/api/portraits/men/1.jpg" } },
    { "234567890123", new AadharHolderData { Name = "Priya Sharma", MobileNo = "9876543211", Address = "456, Park Street, Kolkata, West Bengal - 700016", Gender = "Female", DateOfBirth = "1985-08-22", PhotoUrl = "https://randomuser.me/api/portraits/women/2.jpg" } },
    { "345678901234", new AadharHolderData { Name = "Amit Patel", MobileNo = "9876543212", Address = "789, SG Highway, Ahmedabad, Gujarat - 380015", Gender = "Male", DateOfBirth = "1992-03-10", PhotoUrl = "https://randomuser.me/api/portraits/men/3.jpg" } },
    { "456789012345", new AadharHolderData { Name = "Sneha Reddy", MobileNo = "9876543213", Address = "321, Banjara Hills, Hyderabad, Telangana - 500034", Gender = "Female", DateOfBirth = "1988-11-05", PhotoUrl = "https://randomuser.me/api/portraits/women/4.jpg" } },
    { "567890123456", new AadharHolderData { Name = "Vikram Singh", MobileNo = "9876543214", Address = "654, Connaught Place, New Delhi, Delhi - 110001", Gender = "Male", DateOfBirth = "1995-01-20", PhotoUrl = "https://randomuser.me/api/portraits/men/5.jpg" } },
    { "678901234567", new AadharHolderData { Name = "Anjali Mehta", MobileNo = "9876543215", Address = "987, Marine Drive, Mumbai, Maharashtra - 400002", Gender = "Female", DateOfBirth = "1991-07-18", PhotoUrl = "https://randomuser.me/api/portraits/women/6.jpg" } },
    { "789012345678", new AadharHolderData { Name = "Karthik Iyer", MobileNo = "9876543216", Address = "147, Anna Salai, Chennai, Tamil Nadu - 600002", Gender = "Male", DateOfBirth = "1987-09-25", PhotoUrl = "https://randomuser.me/api/portraits/men/7.jpg" } },
    { "890123456789", new AadharHolderData { Name = "Divya Nair", MobileNo = "9876543217", Address = "258, MG Road, Kochi, Kerala - 682016", Gender = "Female", DateOfBirth = "1993-04-12", PhotoUrl = "https://randomuser.me/api/portraits/women/8.jpg" } },
    { "901234567890", new AadharHolderData { Name = "Arjun Desai", MobileNo = "9876543218", Address = "369, FC Road, Pune, Maharashtra - 411004", Gender = "Male", DateOfBirth = "1989-12-08", PhotoUrl = "https://randomuser.me/api/portraits/men/9.jpg" } },
    { "012345678901", new AadharHolderData { Name = "Meera Kapoor", MobileNo = "9876543219", Address = "741, Mall Road, Shimla, Himachal Pradesh - 171001", Gender = "Female", DateOfBirth = "1994-06-30", PhotoUrl = "https://randomuser.me/api/portraits/women/10.jpg" } },

    // Under 18
    { "112233445566", new AadharHolderData { Name = "Aarav Malhotra", MobileNo = "9876543220", Address = "18, Residency Road, Bengaluru, Karnataka - 560025", Gender = "Male", DateOfBirth = "2010-02-14", PhotoUrl = "https://randomuser.me/api/portraits/men/11.jpg" } },
    { "223344556677", new AadharHolderData { Name = "Kiara Joshi", MobileNo = "9876543221", Address = "42, Koregaon Park, Pune, Maharashtra - 411001", Gender = "Female", DateOfBirth = "2012-06-27", PhotoUrl = "https://randomuser.me/api/portraits/women/12.jpg" } },
    { "334455667788", new AadharHolderData { Name = "Rohan Chatterjee", MobileNo = "9876543222", Address = "76, Salt Lake City, Kolkata, West Bengal - 700091", Gender = "Male", DateOfBirth = "2011-10-09", PhotoUrl = "https://randomuser.me/api/portraits/men/13.jpg" } },
    { "445566778899", new AadharHolderData { Name = "Myra Krishnan", MobileNo = "9876543223", Address = "25, Adyar Main Road, Chennai, Tamil Nadu - 600020", Gender = "Female", DateOfBirth = "2014-03-18", PhotoUrl = "https://randomuser.me/api/portraits/women/14.jpg" } },
    { "556677889900", new AadharHolderData { Name = "Devansh Gupta", MobileNo = "9876543224", Address = "63, Gomti Nagar, Lucknow, Uttar Pradesh - 226010", Gender = "Male", DateOfBirth = "2009-12-21", PhotoUrl = "https://randomuser.me/api/portraits/men/15.jpg" } }
};


    public static readonly IReadOnlyList<string> ValidAadharNumbers = AadharDatabase.Keys.OrderBy(k => k).ToList();

    public AadharVerificationService(ILogger<AadharVerificationService> logger)
    {
        _logger = logger;
    }

    public async Task<AadharVerificationResponse> VerifyAsync(string aadharNumber)
    {
        var obfuscated = aadharNumber.Length >= 4 ? $"{aadharNumber.Substring(0, 4)}********" : "********";
        _logger.LogInformation("Verifying Aadhar number: {Aadhar}", obfuscated);

        // Simulate network latency
        await Task.Delay(100);

        if (AadharDatabase.TryGetValue(aadharNumber, out var holderData))
        {
            _logger.LogInformation("Aadhar verification successful: {Aadhar} - {Name}", obfuscated, holderData.Name);
            
            return new AadharVerificationResponse
            {
                AadharNumber = aadharNumber,
                IsValid = true,
                Status = "VERIFIED",
                Message = "Aadhar number verified successfully",
                Name = holderData.Name,
                MobileNo = holderData.MobileNo,
                Address = holderData.Address,
                Gender = holderData.Gender,
                DateOfBirth = holderData.DateOfBirth,
                PhotoUrl = holderData.PhotoUrl,
                Timestamp = DateTime.UtcNow
            };
        }
        else
        {
            _logger.LogWarning("Aadhar verification failed: {Aadhar}", obfuscated);
            return new AadharVerificationResponse
            {
                AadharNumber = aadharNumber,
                IsValid = false,
                Status = "INVALID",
                Message = "Aadhar number not found in UIDAI database",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private class AadharHolderData
    {
        public string Name { get; init; } = null!;
        public string MobileNo { get; init; } = null!;
        public string Address { get; init; } = null!;
        public string Gender { get; init; } = null!;
        public string DateOfBirth { get; init; } = null!;
        public string PhotoUrl { get; init; } = null!;
    }
}
