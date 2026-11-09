using FunEvents.Application.Reservations;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.GetReservation;

public sealed class GetReservationHandler(IReservationRepository reservations)
{
    public async Task<Reservation?> HandleAsync(
        GetReservationQuery query,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await reservations.ExpireIfDueAsync(
            query.ReservationCode,
            query.ChannelId,
            nowUtc,
            cancellationToken);

        return await reservations.GetByCodeAsync(
            query.ReservationCode,
            query.ChannelId,
            cancellationToken);
    }
}
