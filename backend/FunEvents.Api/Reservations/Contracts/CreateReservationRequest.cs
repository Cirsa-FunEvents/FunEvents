namespace FunEvents.Api.Reservations.Contracts;

public sealed record CreateReservationRequest(
    string EventCode,
    string SessionCode,
    string UserCode,
    int Quantity);
