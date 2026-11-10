using FunEvents.Application.Reservations.Repositories;
using FunEvents.Application.Reservations.CancelReservation.Dtos;
using FunEvents.Application.Reservations.Dtos;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CancelReservation;

public sealed class CancelReservationUseCase(IReservationRepository reservations)
{
    public async Task<CancelReservationResult> ExecuteAsync(
        CancelReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var reservation = await reservations.CancelAsync(
            input.ReservationCode,
            input.ChannelId,
            nowUtc,
            cancellationToken);

        return new(reservation is null ? null : ReservationDetails.From(reservation));
    }
}
