using Gdb.Common.Security;
using Microsoft.AspNetCore.Mvc;
using AadharService.DTOs;
using AadharService.Services;
using AadharService.Config;

namespace AadharService.Controllers;

public class ValidNumbersResponse
{
    public List<string> valid_aadhar_numbers { get; set; } = new();
    public int count { get; set; }
}

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class AadharController : ControllerBase
{
    private readonly AadharVerificationService _service;

    public AadharController(AadharVerificationService service)
    {
        _service = service;
    }

    [HttpGet("verify/{aadhar_number}")]
    public async Task<ActionResult<AadharVerificationResponse>> VerifyAadharGet([FromRoute(Name = "aadhar_number")] string aadharNumber)
    {
        if (aadharNumber == null || aadharNumber.Length != 12 || !aadharNumber.All(char.IsDigit))
        {
            return BadRequest(new { error_code = "VERIFICATION_ERROR", message = "Aadhar number must be exactly 12 digits" });
        }

        try
        {
            var result = await _service.VerifyAsync(aadharNumber);
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = "Internal server error" });
        }
    }

    [HttpPost("verify")]
    public async Task<ActionResult<AadharVerificationResponse>> VerifyAadharPost([FromBody] AadharVerificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error_code = "VERIFICATION_ERROR", message = "Aadhar number must be exactly 12 digits" });
        }

        try
        {
            var result = await _service.VerifyAsync(request.AadharNumber);
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { error_code = "INTERNAL_ERROR", message = "Internal server error" });
        }
    }

    [HttpGet("valid-numbers")]
    [ProducesResponseType(typeof(ValidNumbersResponse), 200)]
    public ActionResult<ValidNumbersResponse> GetValidNumbers()
    {
        return Ok(new ValidNumbersResponse
        {
            valid_aadhar_numbers = AadharVerificationService.ValidAadharNumbers.ToList(),
            count = AadharVerificationService.ValidAadharNumbers.Count
        });
    }
}

