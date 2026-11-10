using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Repositories.Reservations;

public interface IReservationRepository
{
    Task<Reservation?> GetByCodeAsync(
        string reservationCode,
        string channelId,
        CancellationToken cancellationToken);

    Task<ReservationIdempotencyRecord?> GetIdempotencyRecordAsync(
        string channelId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReservationExpiryCandidate>> GetDueForExpiryAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);

    void Add(Reservation reservation);
    void AddIdempotencyRecord(ReservationIdempotencyRecord record);
}

public sealed record ReservationIdempotencyRecord(
    string ChannelId,
    string IdempotencyKey,
    string RequestFingerprint,
    string ReservationCode,
    DateTimeOffset CreatedAtUtc);

public sealed record ReservationExpiryCandidate(string ReservationCode, string ChannelId);
