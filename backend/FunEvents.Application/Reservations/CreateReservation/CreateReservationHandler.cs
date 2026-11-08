using FunEvents.Application.Reservations;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CreateReservation;

public sealed class CreateReservationHandler(
    IReservationRepository reservations,
    ReservationPolicy policy)
{
    public async Task<CreateReservationResult> HandleAsync(
        CreateReservationCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (command.Quantity <= 0 || command.Quantity > policy.MaxQuantityPerReservation)
            return new(null, CreateReservationFailure.InvalidQuantity);

        var reservation = Reservation.Create(
            command.EventCode,
            command.SessionCode,
            command.UserCode,
            command.ChannelId,
            command.Quantity,
            nowUtc,
            policy.HoldDuration);

        var fingerprint = RequestFingerprint.Create(
            command.EventCode, command.SessionCode, command.UserCode, command.Quantity);

        return await reservations.TryCreateAsync(
            reservation,
            command.IdempotencyKey,
            fingerprint,
            cancellationToken);
    }
}
