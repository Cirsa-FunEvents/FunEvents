using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CreateReservation.Dtos;

public enum CreateReservationFailure
{
    InvalidQuantity,
    EventNotFound,
    SessionNotFound,
    UserNotFound,
    SessionUnavailable,
    InsufficientInventory,
    IdempotencyKeyReused
}

public sealed record CreateReservationResult(
    Reservation? Reservation,
    CreateReservationFailure? Failure = null,
    bool IsReplay = false)
{
    public bool Succeeded => Reservation is not null;
}
