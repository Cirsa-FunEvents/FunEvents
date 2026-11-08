using FunEvents.Application.Reservations;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CancelReservation;

public sealed class CancelReservationHandler(IReservationRepository reservations)
{
    public Task<Reservation?> HandleAsync(
        CancelReservationCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        reservations.CancelAsync(
            command.ReservationCode,
            command.ChannelId,
            nowUtc,
            cancellationToken);
}
