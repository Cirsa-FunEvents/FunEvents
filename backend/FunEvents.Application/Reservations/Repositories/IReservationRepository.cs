using FunEvents.Domain.Reservations;
using FunEvents.Application.Reservations.Repositories;

namespace FunEvents.Application.Reservations.Repositories;

public interface IReservationRepository
{
    Task<CreateReservationOutcome> TryCreateAsync(
        Reservation reservation,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken);

    Task<Reservation?> GetByCodeAsync(
        string reservationCode,
        string channelId,
        CancellationToken cancellationToken);

    Task<bool> ExpireIfDueAsync(
        string reservationCode,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<Reservation?> CancelAsync(
        string reservationCode,
        string channelId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<int> ExpireDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);
}
