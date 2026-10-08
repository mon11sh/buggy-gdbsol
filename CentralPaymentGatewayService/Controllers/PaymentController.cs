using Gdb.Common.Security;
using Microsoft.AspNetCore.Mvc;
using CentralPaymentGatewayService.DTOs;
using CentralPaymentGatewayService.Services;


namespace CentralPaymentGatewayService.Controllers;

[ApiController]
[Route("api/v1/payment")]
[InternalApi]
public class PaymentController : ControllerBase
{
    private readonly PaymentGatewayService _service;

    public PaymentController(PaymentGatewayService service)
    {
        _service = service;
    }

    [HttpPost("process")]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment([FromBody] PaymentRequest request)
    {
        var response = await _service.ProcessPaymentAsync(request);
        return Ok(response);
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidationResponse>> ValidatePayment([FromBody] ValidationRequest request)
    {
        var response = await _service.ValidateTransferAsync(request);
        return Ok(response);
    }
}


