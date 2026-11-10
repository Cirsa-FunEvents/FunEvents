using FunEvents.Application.Repositories.Reservations;
using FunEvents.Domain.Reservations;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Repositories.Reservations;

public sealed class ReservationRepository(FunEventsDbContext db) : IReservationRepository
{
    public Task<Reservation?> GetByCodeAsync(
        string reservationCode,
        string channelId,
        CancellationToken cancellationToken) =>
        db.Reservations.SingleOrDefaultAsync(
            x => x.ReservationCode == reservationCode && x.ChannelId == channelId,
            cancellationToken);

    public async Task<ReservationIdempotencyRecord?> GetIdempotencyRecordAsync(
        string channelId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            x => x.ChannelId == channelId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);
        return record is null
            ? null
            : new(record.ChannelId, record.IdempotencyKey, record.RequestFingerprint,
                record.ReservationCode, record.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ReservationExpiryCandidate>> GetDueForExpiryAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken) =>
        await db.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Pending && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .Select(x => new ReservationExpiryCandidate(x.ReservationCode, x.ChannelId))
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);

    public void AddIdempotencyRecord(ReservationIdempotencyRecord record) =>
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ChannelId = record.ChannelId,
            IdempotencyKey = record.IdempotencyKey,
            RequestFingerprint = record.RequestFingerprint,
            ReservationCode = record.ReservationCode,
            CreatedAtUtc = record.CreatedAtUtc
        });
}
