namespace FunEvents.Application.Reservations;

public sealed record ReservationPolicy(TimeSpan HoldDuration, int MaxQuantityPerReservation);
