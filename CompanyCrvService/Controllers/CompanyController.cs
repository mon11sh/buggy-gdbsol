using Gdb.Common.Security;
using Microsoft.AspNetCore.Mvc;
using CompanyCrvService.DTOs;
using CompanyCrvService.Services;

namespace CompanyCrvService.Controllers;

public class ValidCompaniesResponse
{
    public List<string> valid_companies { get; set; } = new();
    public int count { get; set; }
}

[ApiController]
[Route("api/v1/company")]
[Produces("application/json")]
public class CompanyController : ControllerBase
{
    private readonly CompanyVerificationService _service;

    public CompanyController(CompanyVerificationService service)
    {
        _service = service;
    }

    [HttpGet("verify/{registration_number}")]
    public async Task<ActionResult<CompanyVerificationResponse>> VerifyCompanyGet([FromRoute(Name = "registration_number")] string registrationNumber)
    {

        try
        {
            var result = await _service.VerifyAsync(registrationNumber.ToUpper());
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = "Internal server error" });
        }
    }

    [HttpPost("verify")]
    public async Task<ActionResult<CompanyVerificationResponse>> VerifyCompanyPost([FromBody] CompanyVerificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errorMessage = "Registration number must be exactly 21 characters";
            if (request.RegistrationNumber?.Length == 21 && !request.RegistrationNumber.All(char.IsLetterOrDigit))
            {
                errorMessage = "Registration number must be alphanumeric";
            }
            return BadRequest(new { error_code = "VERIFICATION_ERROR", message = errorMessage });
        }

        try
        {
            var result = await _service.VerifyAsync(request.RegistrationNumber);
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = "Internal server error" });
        }
    }

    [HttpGet("valid-companies")]
    [ProducesResponseType(typeof(ValidCompaniesResponse), 200)]
    public ActionResult<ValidCompaniesResponse> GetValidCompanies()
    {
        return Ok(new ValidCompaniesResponse
        {
            valid_companies = CompanyVerificationService.ValidRegistrationNumbers.ToList(),
            count = CompanyVerificationService.ValidRegistrationNumbers.Count
        });
    }
}

