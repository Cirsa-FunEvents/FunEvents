namespace FunEvents.Application.Payments.ProcessPayment.Dtos;

public enum PaymentFailure
{
    ReservationNotFound,
    ReservationNotPayable,
    PaymentAlreadyPending,
    IdempotencyKeyReused
}
