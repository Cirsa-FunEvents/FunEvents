using FunEvents.Domain.Reservations;
using FunEvents.Application.Reservations.CreateReservation;

namespace FunEvents.Application.Reservations;

public interface IReservationRepository
{
    Task<CreateReservationResult> TryCreateAsync(
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
