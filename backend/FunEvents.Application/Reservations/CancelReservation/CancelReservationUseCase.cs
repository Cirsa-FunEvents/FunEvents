using FunEvents.Application.Reservations;
using FunEvents.Application.Reservations.CancelReservation.Dtos;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CancelReservation;

public sealed class CancelReservationUseCase(IReservationRepository reservations)
{
    public Task<Reservation?> ExecuteAsync(
        CancelReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        reservations.CancelAsync(
            input.ReservationCode,
            input.ChannelId,
            nowUtc,
            cancellationToken);
}
