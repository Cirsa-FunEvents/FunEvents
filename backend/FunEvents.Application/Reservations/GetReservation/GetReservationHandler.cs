using FunEvents.Application.Reservations;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.GetReservation;

public sealed class GetReservationHandler(IReservationRepository reservations)
{
    public Task<Reservation?> HandleAsync(
        GetReservationQuery query,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        reservations.GetByCodeAsync(
            query.ReservationCode,
            query.ChannelId,
            nowUtc,
            cancellationToken);
}
