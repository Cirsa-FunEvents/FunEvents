using FunEvents.Application.Reservations.Dtos;

namespace FunEvents.Application.Reservations.GetReservation.Dtos;

public sealed record GetReservationResult(ReservationDetails? Reservation);
