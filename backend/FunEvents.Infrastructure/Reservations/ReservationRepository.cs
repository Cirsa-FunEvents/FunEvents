using FunEvents.Application.Reservations;
using FunEvents.Application.Reservations.CreateReservation;
using FunEvents.Domain.Reservations;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FunEvents.Infrastructure.Reservations;

public sealed class ReservationRepository(FunEventsDbContext db) : IReservationRepository
{
    public async Task<CreateReservationResult> TryCreateAsync(
        Reservation reservation,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var previous = await FindIdempotentResultAsync(
                reservation.ChannelId, idempotencyKey, requestFingerprint, cancellationToken);
            if (previous is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return previous;
            }

            var eventExists = await db.Events.AnyAsync(
                x => x.EventCode == reservation.EventCode && x.IsActive,
                cancellationToken);
            if (!eventExists)
                return new(null, CreateReservationFailure.EventNotFound);

            var session = await db.EventSessions.SingleOrDefaultAsync(
                x => x.EventCode == reservation.EventCode && x.SessionCode == reservation.SessionCode,
                cancellationToken);
            if (session is null)
                return new(null, CreateReservationFailure.SessionNotFound);
            if (!session.IsSalesOpen || session.StartsAtUtc <= reservation.CreatedAtUtc)
                return new(null, CreateReservationFailure.SessionUnavailable);

            var userExists = await db.Users.AnyAsync(
                x => x.UserCode == reservation.UserCode,
                cancellationToken);
            if (!userExists)
                return new(null, CreateReservationFailure.UserNotFound);

            var inventoryChanged = await db.SessionInventory
                .Where(x => x.EventCode == reservation.EventCode
                    && x.SessionCode == reservation.SessionCode
                    && x.AvailableQuantity >= reservation.Quantity)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    x => x.AvailableQuantity,
                    x => x.AvailableQuantity - reservation.Quantity), cancellationToken);

            if (inventoryChanged == 0)
            {
                var replay = await FindIdempotentResultAsync(
                    reservation.ChannelId, idempotencyKey, requestFingerprint, cancellationToken);
                if (replay is not null)
                    return replay;
                return new(null, CreateReservationFailure.InsufficientInventory);
            }

            db.Reservations.Add(reservation);
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                ChannelId = reservation.ChannelId,
                IdempotencyKey = idempotencyKey,
                RequestFingerprint = requestFingerprint,
                ReservationCode = reservation.ReservationCode,
                CreatedAtUtc = reservation.CreatedAtUtc
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(reservation);
        }
        catch (DbUpdateException exception) when (IsIdempotencyKeyCollision(exception))
        {
            db.ChangeTracker.Clear();
            var previous = await FindIdempotentResultAsync(
                reservation.ChannelId, idempotencyKey, requestFingerprint, cancellationToken);
            if (previous is null)
                throw;
            return previous;
        }
    }

    public async Task<Reservation?> GetByCodeAsync(
        string reservationCode,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await TransitionToExpiredAsync(reservationCode, channelId, nowUtc, cancellationToken);
        return await db.Reservations.AsNoTracking().SingleOrDefaultAsync(
            x => x.ReservationCode == reservationCode && x.ChannelId == channelId,
            cancellationToken);
    }

    public async Task<Reservation?> CancelAsync(
        string reservationCode,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(
            x => x.ReservationCode == reservationCode && x.ChannelId == channelId,
            cancellationToken);

        if (reservation is null || !reservation.Cancel(nowUtc))
        {
            await transaction.CommitAsync(cancellationToken);
            return reservation;
        }

        var targetStatus = reservation.Status;
        var transition = db.Reservations.Where(x =>
            x.Id == reservation.Id && x.Status == ReservationStatus.Pending);
        transition = targetStatus == ReservationStatus.Expired
            ? transition.Where(x => x.ExpiresAtUtc <= nowUtc)
            : transition.Where(x => x.ExpiresAtUtc > nowUtc);

        var changed = await transition.ExecuteUpdateAsync(
            update => update.SetProperty(x => x.Status, targetStatus), cancellationToken);

        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return await db.Reservations.AsNoTracking().SingleOrDefaultAsync(
                x => x.ReservationCode == reservationCode && x.ChannelId == channelId,
                cancellationToken);
        }

        await ReleaseInventoryAsync(reservation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return await db.Reservations.AsNoTracking().SingleAsync(
            x => x.ReservationCode == reservationCode && x.ChannelId == channelId,
            cancellationToken);
    }

    public async Task<int> ExpireDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var due = await db.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Pending && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .Select(x => new { x.ReservationCode, x.ChannelId })
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var item in due)
        {
            if (await TransitionToExpiredAsync(
                    item.ReservationCode, item.ChannelId, nowUtc, cancellationToken))
                expired++;
        }

        return expired;
    }

    private async Task<bool> TransitionToExpiredAsync(
        string reservationCode,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(
            x => x.ReservationCode == reservationCode && x.ChannelId == channelId,
            cancellationToken);

        if (reservation is null || !reservation.ExpireIfDue(nowUtc))
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var changed = await db.Reservations
            .Where(x => x.Id == reservation.Id
                && x.Status == ReservationStatus.Pending
                && x.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(
                update => update.SetProperty(x => x.Status, ReservationStatus.Expired),
                cancellationToken);

        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return false;
        }

        await ReleaseInventoryAsync(reservation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return true;
    }

    private async Task ReleaseInventoryAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var changed = await db.SessionInventory
            .Where(x => x.EventCode == reservation.EventCode && x.SessionCode == reservation.SessionCode)
            .ExecuteUpdateAsync(update => update.SetProperty(
                x => x.AvailableQuantity,
                x => x.AvailableQuantity + reservation.Quantity), cancellationToken);

        if (changed != 1)
            throw new InvalidOperationException("Inventory row for the reservation was not found.");
    }

    private async Task<CreateReservationResult?> FindIdempotentResultAsync(
        string channelId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var previous = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            x => x.ChannelId == channelId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (previous is null)
            return null;

        if (!string.Equals(previous.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
            return new(null, CreateReservationFailure.IdempotencyKeyReused);

        var reservation = await db.Reservations.AsNoTracking().SingleAsync(
            x => x.ReservationCode == previous.ReservationCode,
            cancellationToken);
        return new(reservation, IsReplay: true);
    }

    private static bool IsIdempotencyKeyCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_reservation_idempotency"
        };
}
