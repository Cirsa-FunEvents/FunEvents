using FunEvents.Application.Common.Persistence;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.SessionInventory;

namespace FunEvents.Application.Reservations.ExpireReservations;

public sealed class ExpireReservationsUseCase(
    IReservationRepository reservations,
    ISessionInventoryRepository inventory,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> ExpireIfDueAsync(
        string reservationCode,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            var reservation = await reservations.GetByCodeAsync(
                reservationCode, channelId, cancellationToken);
            if (reservation is null || !reservation.ExpireIfDue(nowUtc))
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await inventory.ReleaseAsync(
                reservation.EventCode, reservation.SessionCode, reservation.Quantity, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            // A competing cancel, payment, or expiry won. Its transaction owns inventory release.
            unitOfWork.ClearTracking();
            return false;
        }
    }

    public async Task<int> ExpireDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var due = await reservations.GetDueForExpiryAsync(nowUtc, batchSize, cancellationToken);
        var expired = 0;
        foreach (var item in due)
        {
            if (await ExpireIfDueAsync(
                    item.ReservationCode, item.ChannelId, nowUtc, cancellationToken))
                expired++;
        }
        return expired;
    }
}
