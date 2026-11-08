namespace FunEvents.Application.Reservations.CancelReservation;

public sealed record CancelReservationCommand(string ReservationCode, string ChannelId);
