using FunEvents.Domain.Reservations;

namespace FunEvents.Api.Reservations.Contracts;

public sealed record ReservationResponse(
    string ReservationCode,
    string EventCode,
    string SessionCode,
    string UserCode,
    int Quantity,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public static ReservationResponse From(Reservation reservation) => new(
        reservation.ReservationCode,
        reservation.EventCode,
        reservation.SessionCode,
        reservation.UserCode,
        reservation.Quantity,
        reservation.Status.ToString(),
        reservation.CreatedAtUtc,
        reservation.ExpiresAtUtc);
}
