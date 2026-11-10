using FunEvents.Application.Common.Persistence;
using FunEvents.Application.Reservations.CreateReservation.Dtos;
using FunEvents.Application.Reservations.Dtos;
using FunEvents.Application.Reservations.Helpers;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.Users;
using FunEvents.Application.Repositories.SessionInventory;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Reservations.CreateReservation;

public sealed class CreateReservationUseCase(
    IReservationRepository reservations,
    IReservationCatalog catalog,
    IUserRepository users,
    ISessionInventoryRepository inventory,
    IUnitOfWork unitOfWork,
    ReservationPolicy policy)
{
    public async Task<CreateReservationResult> ExecuteAsync(
        CreateReservationInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (input.Quantity <= 0 || input.Quantity > policy.MaxQuantityPerReservation)
            return new(null, CreateReservationFailure.InvalidQuantity);

        var fingerprint = RequestFingerprint.Create(
            input.EventCode, input.SessionCode, input.UserCode, input.Quantity);

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

            var replay = await FindReplayAsync(input, fingerprint, cancellationToken);
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            if (!await catalog.IsActiveEventAsync(input.EventCode, cancellationToken))
                return new(null, CreateReservationFailure.EventNotFound);

            var session = await catalog.GetSessionAsync(
                input.EventCode, input.SessionCode, cancellationToken);
            if (session is null)
                return new(null, CreateReservationFailure.SessionNotFound);
            if (!session.IsSalesOpen || session.StartsAtUtc <= nowUtc)
                return new(null, CreateReservationFailure.SessionUnavailable);

            if (!await users.ExistsAsync(input.UserCode, cancellationToken))
                return new(null, CreateReservationFailure.UserNotFound);

            if (!await inventory.TryHoldAsync(
                    input.EventCode, input.SessionCode, input.Quantity, cancellationToken))
            {
                // Another request with this key may have won while inventory was contended.
                replay = await FindReplayAsync(input, fingerprint, cancellationToken);
                if (replay is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return replay;
                }
                return new(null, CreateReservationFailure.InsufficientInventory);
            }

            var reservation = Reservation.Create(
                input.EventCode,
                input.SessionCode,
                input.UserCode,
                input.ChannelId,
                input.Quantity,
                nowUtc,
                policy.HoldDuration);

            reservations.Add(reservation);
            reservations.AddIdempotencyRecord(new ReservationIdempotencyRecord(
                input.ChannelId,
                input.IdempotencyKey,
                fingerprint,
                reservation.ReservationCode,
                reservation.CreatedAtUtc));

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ReservationDetails.From(reservation));
        }
        catch (PersistenceConflictException)
        {
            unitOfWork.ClearTracking();
            var replay = await FindReplayAsync(input, fingerprint, cancellationToken);
            if (replay is not null)
                return replay;
            throw;
        }
    }

    private async Task<CreateReservationResult?> FindReplayAsync(
        CreateReservationInput input,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var record = await reservations.GetIdempotencyRecordAsync(
            input.ChannelId, input.IdempotencyKey, cancellationToken);
        if (record is null)
            return null;

        if (!string.Equals(record.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            return new(null, CreateReservationFailure.IdempotencyKeyReused);

        var existingReservation = await reservations.GetByCodeAsync(
            record.ReservationCode, input.ChannelId, cancellationToken);
        if (existingReservation is null)
            throw new InvalidOperationException("Idempotency record references a missing reservation.");

        return new(ReservationDetails.From(existingReservation), IsReplay: true);
    }
}
