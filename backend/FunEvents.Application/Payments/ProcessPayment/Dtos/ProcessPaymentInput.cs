namespace FunEvents.Application.Payments.ProcessPayment.Dtos;

public sealed record ProcessPaymentInput(
    string ReservationCode,
    string ChannelId,
    string IdempotencyKey);
