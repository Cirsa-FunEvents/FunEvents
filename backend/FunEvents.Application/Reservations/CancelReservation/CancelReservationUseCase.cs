using FunEvents.Application.Common.Persistence;
using FunEvents.Application.Reservations.CancelReservation.Dtos;
using FunEvents.Application.Reservations.Dtos;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.SessionInventory;

namespace FunEvents.Application.Reservations.CancelReservation;

public sealed class CancelReservationUseCase(
    IReservationRepository reservations,
    ISessionInventoryRepository inventory,
    IUnitOfWork unitOfWork)
{
    public async Task<CancelReservationResult> ExecuteAsync(
        CancelReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            var reservation = await reservations.GetByCodeAsync(
                input.ReservationCode, input.ChannelId, cancellationToken);

            if (reservation is null || !reservation.Cancel(nowUtc))
            {
                await transaction.CommitAsync(cancellationToken);
                return new(reservation is null ? null : ReservationDetails.From(reservation));
            }

            // The status write uses the reservation status concurrency token. Only its winner
            // releases the held inventory, in the same transaction.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await inventory.ReleaseAsync(
                reservation.EventCode, reservation.SessionCode, reservation.Quantity, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ReservationDetails.From(reservation));
        }
        catch (ConcurrencyConflictException)
        {
            unitOfWork.ClearTracking();
            var current = await reservations.GetByCodeAsync(
                input.ReservationCode, input.ChannelId, cancellationToken);
            return new(current is null ? null : ReservationDetails.From(current));
        }
    }
}
