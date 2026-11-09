namespace FunEvents.Application.Reservations.CreateReservation.Dtos;

public sealed record CreateReservationInput(
    string EventCode,
    string SessionCode,
    string UserCode,
    int Quantity,
    string ChannelId,
    string IdempotencyKey);
