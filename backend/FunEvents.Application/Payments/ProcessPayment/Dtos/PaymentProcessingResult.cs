namespace FunEvents.Application.Payments.ProcessPayment.Dtos;

public sealed record PaymentProcessingResult(
    PaymentDetails? Payment,
    PaymentFailure? Failure = null,
    bool IsReplay = false);
