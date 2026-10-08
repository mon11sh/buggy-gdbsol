using CompanyCrvService.DTOs;

namespace CompanyCrvService.Services;

public class CompanyVerificationService
{
    private readonly ILogger<CompanyVerificationService> _logger;

    private static readonly Dictionary<string, CompanyData> ValidCompanies = new()
    {
        { "U72900KA2020PTC123456", new CompanyData { CompanyName = "Tech Innovations Pvt Ltd", Type = "Private Limited", Address = "123 Tech Park, Electronic City, Bangalore, Karnataka - 560100", Email = "info@techinnovations.com", Phone = "+91-80-12345678", Website = "https://techinnovations.com", IncorporationDate = "2020-01-15", PaidUpCapital = "10,00,000", Directors = new List<string> { "Rajesh Kumar", "Priya Sharma" } } },
        { "L67120MH2019PLC234567", new CompanyData { CompanyName = "Global Finance Ltd", Type = "Public Limited", Address = "456 Financial District, BKC, Mumbai, Maharashtra - 400051", Email = "contact@globalfinance.com", Phone = "+91-22-23456789", Website = "https://globalfinance.com", IncorporationDate = "2019-06-20", PaidUpCapital = "50,00,000", Directors = new List<string> { "Suresh Menon", "Anita Desai", "Vikram Singh" } } },
        { "U74999DL2021PTC345678", new CompanyData { CompanyName = "Digital Solutions Pvt Ltd", Type = "Private Limited", Address = "789 Cyber Hub, Gurugram, Delhi NCR - 122002", Email = "hello@digitalsolutions.in", Phone = "+91-124-3456789", Website = "https://digitalsolutions.in", IncorporationDate = "2021-03-10", PaidUpCapital = "25,00,000", Directors = new List<string> { "Amit Patel", "Sneha Reddy" } } },
        { "L85110TN2018PLC456789", new CompanyData { CompanyName = "Manufacturing Excellence Ltd", Type = "Public Limited", Address = "Plot 100, SIPCOT Industrial Park, Chennai, Tamil Nadu - 600058", Email = "info@manufacturingexcellence.com", Phone = "+91-44-45678901", Website = "https://manufacturingexcellence.com", IncorporationDate = "2018-09-05", PaidUpCapital = "1,00,00,000", Directors = new List<string> { "Rahul Sharma", "Deepika Kapoor", "Manish Gupta" } } },
        { "U51909GJ2022PTC567890", new CompanyData { CompanyName = "Retail Ventures Pvt Ltd", Type = "Private Limited", Address = "55 Commerce Center, Ahmedabad, Gujarat - 380009", Email = "support@retailventures.co.in", Phone = "+91-79-56789012", Website = "https://retailventures.co.in", IncorporationDate = "2022-02-28", PaidUpCapital = "15,00,000", Directors = new List<string> { "Kiran Patel", "Meera Shah" } } },
        { "L24233WB2017PLC678901", new CompanyData { CompanyName = "Eastern Chemicals Ltd", Type = "Public Limited", Address = "Industrial Area, Durgapur, West Bengal - 713213", Email = "contact@easternchemicals.com", Phone = "+91-343-6789012", Website = "https://easternchemicals.com", IncorporationDate = "2017-11-15", PaidUpCapital = "75,00,000", Directors = new List<string> { "Arun Banerjee", "Suman Roy", "Priyanka Das" } } },
        { "U45200HR2023PTC789012", new CompanyData { CompanyName = "Green Energy Solutions Pvt Ltd", Type = "Private Limited", Address = "Eco Park, Sector 62, Faridabad, Haryana - 121004", Email = "info@greenenergysolutions.in", Phone = "+91-129-7890123", Website = "https://greenenergysolutions.in", IncorporationDate = "2023-01-20", PaidUpCapital = "30,00,000", Directors = new List<string> { "Vivek Tiwari", "Neha Agarwal" } } },
        { "L29130AP2019PLC890123", new CompanyData { CompanyName = "Pharma Health Ltd", Type = "Public Limited", Address = "Pharma City, Visakhapatnam, Andhra Pradesh - 530046", Email = "corporate@pharmahealth.com", Phone = "+91-891-8901234", Website = "https://pharmahealth.com", IncorporationDate = "2019-08-12", PaidUpCapital = "2,00,00,000", Directors = new List<string> { "Dr. Ramesh Naidu", "Dr. Lakshmi Devi", "Srinivas Rao" } } },
        { "U62013RJ2021PTC901234", new CompanyData { CompanyName = "Textile Creations Pvt Ltd", Type = "Private Limited", Address = "Textile Market, Jaipur, Rajasthan - 302001", Email = "sales@textilecreations.in", Phone = "+91-141-9012345", Website = "https://textilecreations.in", IncorporationDate = "2021-05-30", PaidUpCapital = "20,00,000", Directors = new List<string> { "Mahesh Jain", "Rekha Agarwal" } } },
        { "L15142UP2020PLC012345", new CompanyData { CompanyName = "Food Processing Industries Ltd", Type = "Public Limited", Address = "Food Park, Greater Noida, Uttar Pradesh - 201310", Email = "info@foodprocessing.com", Phone = "+91-120-0123456", Website = "https://foodprocessing.com", IncorporationDate = "2020-04-18", PaidUpCapital = "1,50,00,000", Directors = new List<string> { "Rajendra Singh", "Kavita Verma", "Alok Kumar" } } }
    };

    public static readonly IReadOnlyList<string> ValidRegistrationNumbers = ValidCompanies.Keys.OrderBy(k => k).ToList();

    public CompanyVerificationService(ILogger<CompanyVerificationService> logger)
    {
        _logger = logger;
    }

    public async Task<CompanyVerificationResponse> VerifyAsync(string registrationNumber)
    {
        var obfuscated = registrationNumber.Length >= 8 ? $"{registrationNumber.Substring(0, 8)}*************" : "*************";
        _logger.LogInformation("Verifying company registration: {RegistrationNumber}", obfuscated);

        // Simulate network latency
        await Task.Delay(100);

        if (ValidCompanies.TryGetValue(registrationNumber, out var companyData))
        {
            _logger.LogInformation("Company verification successful: {RegistrationNumber}", obfuscated);
            
            return new CompanyVerificationResponse
            {
                RegistrationNumber = registrationNumber,
                IsValid = true,
                Status = "VERIFIED",
                Message = "Company registration number verified successfully",
                Timestamp = DateTime.UtcNow,
                CompanyName = companyData.CompanyName,
                Type = companyData.Type,
                Address = companyData.Address,
                Email = companyData.Email,
                Phone = companyData.Phone,
                Website = companyData.Website,
                IncorporationDate = companyData.IncorporationDate,
                PaidUpCapital = companyData.PaidUpCapital,
                Directors = companyData.Directors
            };
        }
        else
        {
            _logger.LogWarning("Company verification failed: {RegistrationNumber}", obfuscated);
            return new CompanyVerificationResponse
            {
                RegistrationNumber = registrationNumber,
                IsValid = false,
                Status = "INVALID",
                Message = "Company registration number not found in MCA records",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private class CompanyData
    {
        public string CompanyName { get; init; } = null!;
        public string Type { get; init; } = null!;
        public string Address { get; init; } = null!;
        public string Email { get; init; } = null!;
        public string Phone { get; init; } = null!;
        public string Website { get; init; } = null!;
        public string IncorporationDate { get; init; } = null!;
        public string PaidUpCapital { get; init; } = null!;
        public List<string> Directors { get; init; } = new();
    }
}
