using FunEvents.Application.Reservations.Dtos;

namespace FunEvents.Application.Reservations.CancelReservation.Dtos;

public sealed record CancelReservationResult(ReservationDetails? Reservation);
