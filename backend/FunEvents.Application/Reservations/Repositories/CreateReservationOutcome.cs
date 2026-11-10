using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.Repositories;

public sealed record CreateReservationOutcome(
    Reservation? Reservation,
    CreateReservationFailure? Failure = null,
    bool IsReplay = false);
