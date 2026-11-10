using FunEvents.Application.Reservations.Dtos;

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
    public static ReservationResponse From(ReservationDetails reservation) => new(
        reservation.ReservationCode,
        reservation.EventCode,
        reservation.SessionCode,
        reservation.UserCode,
        reservation.Quantity,
        reservation.Status.ToString(),
        reservation.CreatedAt,
        reservation.ExpiresAt);
}
