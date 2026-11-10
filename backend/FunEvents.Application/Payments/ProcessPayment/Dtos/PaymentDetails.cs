using FunEvents.Domain.Payments;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Payments.ProcessPayment.Dtos;

public sealed record PaymentDetails(
    string PaymentCode,
    string ReservationCode,
    PaymentStatus PaymentStatus,
    ReservationStatus ReservationStatus,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static PaymentDetails From(PaymentAttempt payment, ReservationStatus reservationStatus) => new(
        payment.PaymentCode,
        payment.ReservationCode,
        payment.Status,
        reservationStatus,
        payment.ProviderReference,
        payment.CreatedAtUtc,
        payment.UpdatedAtUtc);
}
