namespace FunEvents.Application.Reservations.CancelReservation.Dtos;

public sealed record CancelReservationInput(string ReservationCode, string ChannelId);
