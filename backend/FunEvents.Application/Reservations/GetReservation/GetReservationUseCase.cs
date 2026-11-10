using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Reservations.ExpireReservations;
using FunEvents.Application.Reservations.GetReservation.Dtos;
using FunEvents.Application.Reservations.Dtos;

namespace FunEvents.Application.Reservations.GetReservation;

public sealed class GetReservationUseCase(
    IReservationRepository reservations,
    ExpireReservationsUseCase expiry)
{
    public async Task<GetReservationResult> ExecuteAsync(
        GetReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await expiry.ExpireIfDueAsync(
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
