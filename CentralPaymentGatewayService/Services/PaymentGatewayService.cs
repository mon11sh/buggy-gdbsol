using CentralPaymentGatewayService.DTOs;

namespace CentralPaymentGatewayService.Services;

public class PaymentGatewayService
{
    private readonly Dictionary<string, PaymentResponse> _processed = new();
    private readonly ILogger<PaymentGatewayService> _logger;
    private readonly Random _random = new();

    public PaymentGatewayService(ILogger<PaymentGatewayService> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        if (!string.IsNullOrEmpty(request.ReferenceId) && _processed.TryGetValue(request.ReferenceId, out var cachedResponse))
        {
            _logger.LogInformation("[IDEMPOTENT REPLAY] reference_id={ReferenceId} returning cached response", request.ReferenceId);
            return cachedResponse;
        }

        int delayMs = _random.Next(100, 501);
        await Task.Delay(delayMs);

        decimal amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        string mode = request.Mode.ToString();

        if (request.SourceAccountId == request.DestinationAccountId)
        {
            var rejectResponse = new PaymentResponse
            {
                Success = false,
                TransactionId = request.ReferenceId ?? Guid.NewGuid().ToString(),
                Message = "Invalid Transaction: Source and Destination cannot be same",
                GatewayRefId = Guid.NewGuid().ToString()
            };
            _logger.LogWarning("[PAYMENT REJECTED] reference_id={ReferenceId} source={Source} dest={Dest} amount={Amount} mode={Mode} result={Result}",
                request.ReferenceId, request.SourceAccountId, request.DestinationAccountId, amount, mode, rejectResponse.Message);
            return Remember(request.ReferenceId, rejectResponse);
        }

        if (_random.NextDouble() < 0.01)
        {
            var errorResponse = new PaymentResponse
            {
                Success = false,
                TransactionId = request.ReferenceId ?? Guid.NewGuid().ToString(),
                Message = "Gateway Error: Network Timeout",
                GatewayRefId = Guid.NewGuid().ToString()
            };
            _logger.LogWarning("[PAYMENT REJECTED] reference_id={ReferenceId} source={Source} dest={Dest} amount={Amount} mode={Mode} result={Result}",
                request.ReferenceId, request.SourceAccountId, request.DestinationAccountId, amount, mode, errorResponse.Message);
            return Remember(request.ReferenceId, errorResponse);
        }

        var successResponse = new PaymentResponse
        {
            Success = true,
            TransactionId = request.ReferenceId ?? Guid.NewGuid().ToString(),
            Message = "Payment Processed Successfully",
            GatewayRefId = Guid.NewGuid().ToString()
        };
        _logger.LogInformation("[PAYMENT PROCESSED] reference_id={ReferenceId} source={Source} dest={Dest} amount={Amount} mode={Mode} result={Result}",
            request.ReferenceId, request.SourceAccountId, request.DestinationAccountId, amount, mode, successResponse.Message);
        
        return Remember(request.ReferenceId, successResponse);
    }

    public async Task<ValidationResponse> ValidateTransferAsync(ValidationRequest request)
    {
        bool valid = request.FromAccount != request.ToAccount;
        string message = valid ? "Validation passed" : "Invalid: source and destination cannot be the same";

        _logger.LogInformation("[PAYMENT VALIDATED] transaction_id={TransactionId} from={From} to={To} amount={Amount} valid={Valid}",
            request.TransactionId, request.FromAccount, request.ToAccount, request.Amount, valid);

        return await Task.FromResult(new ValidationResponse
        {
            Valid = valid,
            Message = message,
            TransactionId = request.TransactionId
        });
    }

    private PaymentResponse Remember(string? referenceId, PaymentResponse response)
    {
        if (!string.IsNullOrEmpty(referenceId))
        {
            _processed[referenceId] = response;
        }
        return response;
    }
}
