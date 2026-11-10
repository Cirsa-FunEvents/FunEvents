using FunEvents.Application.Reservations.Dtos;
using FunEvents.Application.Reservations.Repositories;

namespace FunEvents.Application.Reservations.CreateReservation.Dtos;

public sealed record CreateReservationResult(
    ReservationDetails? Reservation,
    CreateReservationFailure? Failure = null,
    bool IsReplay = false)
{
    public bool Succeeded => Reservation is not null;
}
