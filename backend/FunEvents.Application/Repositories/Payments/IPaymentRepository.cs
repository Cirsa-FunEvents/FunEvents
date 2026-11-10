using FunEvents.Domain.Payments;

namespace FunEvents.Application.Repositories.Payments;

public interface IPaymentRepository
{
    Task<PaymentAttempt?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<PaymentAttempt?> GetByIdempotencyKeyAsync(
        string channelId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> HasPendingForReservationAsync(
        string reservationCode,
        CancellationToken cancellationToken);

    void Add(PaymentAttempt payment);
}
