using FunEvents.Application.Repositories.Payments;
using FunEvents.Domain.Payments;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Repositories.Payments;

public sealed class PaymentRepository(FunEventsDbContext db) : IPaymentRepository
{
    public Task<PaymentAttempt?> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken) =>
        db.PaymentAttempts.SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

    public Task<PaymentAttempt?> GetByIdempotencyKeyAsync(
        string channelId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        db.PaymentAttempts.SingleOrDefaultAsync(
            x => x.ChannelId == channelId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<bool> HasPendingForReservationAsync(
        string reservationCode,
        CancellationToken cancellationToken) =>
        db.PaymentAttempts.AnyAsync(
            x => x.ReservationCode == reservationCode && x.Status == PaymentStatus.Pending,
            cancellationToken);

    public void Add(PaymentAttempt payment) => db.PaymentAttempts.Add(payment);
}
