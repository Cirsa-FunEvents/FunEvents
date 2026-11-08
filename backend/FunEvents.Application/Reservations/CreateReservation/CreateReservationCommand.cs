namespace FunEvents.Application.Reservations.CreateReservation;

public sealed record CreateReservationCommand(
    string EventCode,
    string SessionCode,
    string UserCode,
    int Quantity,
    string ChannelId,
    string IdempotencyKey);
