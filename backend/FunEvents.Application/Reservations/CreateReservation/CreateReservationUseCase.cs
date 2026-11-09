using FunEvents.Application.Reservations;
using FunEvents.Application.Reservations.CreateReservation.Dtos;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CreateReservation;

public sealed class CreateReservationUseCase(
    IReservationRepository reservations,
    ReservationPolicy policy)
{
    public async Task<CreateReservationResult> ExecuteAsync(
        CreateReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (input.Quantity <= 0 || input.Quantity > policy.MaxQuantityPerReservation)
            return new(null, CreateReservationFailure.InvalidQuantity);

        var reservation = Reservation.Create(
            input.EventCode,
            input.SessionCode,
            input.UserCode,
            input.ChannelId,
            input.Quantity,
            nowUtc,
            policy.HoldDuration);

        var fingerprint = RequestFingerprint.Create(
            input.EventCode, input.SessionCode, input.UserCode, input.Quantity);

        return await reservations.TryCreateAsync(
            reservation,
            input.IdempotencyKey,
            fingerprint,
            cancellationToken);
    }
}
