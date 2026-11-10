using FunEvents.Application.Reservations.Repositories;
using FunEvents.Application.Reservations.GetReservation.Dtos;
using FunEvents.Application.Reservations.Dtos;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.GetReservation;

public sealed class GetReservationUseCase(IReservationRepository reservations)
{
    public async Task<GetReservationResult> ExecuteAsync(
        GetReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await reservations.ExpireIfDueAsync(
            input.ReservationCode,
            input.ChannelId,
            nowUtc,
            cancellationToken);

        var reservation = await reservations.GetByCodeAsync(
            input.ReservationCode,
            input.ChannelId,
            cancellationToken);

        return new(reservation is null ? null : ReservationDetails.From(reservation));
    }
}
