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
