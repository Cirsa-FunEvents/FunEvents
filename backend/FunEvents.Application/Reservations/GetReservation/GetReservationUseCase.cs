using FunEvents.Application.Reservations;
using FunEvents.Application.Reservations.GetReservation.Dtos;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.GetReservation;

public sealed class GetReservationUseCase(IReservationRepository reservations)
{
    public async Task<Reservation?> ExecuteAsync(
        GetReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await reservations.ExpireIfDueAsync(
            input.ReservationCode,
            input.ChannelId,
            nowUtc,
            cancellationToken);

        return await reservations.GetByCodeAsync(
            input.ReservationCode,
            input.ChannelId,
            cancellationToken);
    }
}
