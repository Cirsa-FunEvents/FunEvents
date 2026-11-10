namespace FunEvents.Application.Reservations.Repositories;

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
