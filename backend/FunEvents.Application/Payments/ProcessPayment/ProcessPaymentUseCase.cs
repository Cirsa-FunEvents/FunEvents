using FunEvents.Application.Common.Persistence;
using FunEvents.Application.Payments.ProcessPayment.Dtos;
using FunEvents.Application.Repositories.Payments;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Reservations.ExpireReservations;
using FunEvents.Domain.Payments;
using FunEvents.Domain.Reservations;

namespace FunEvents.Application.Payments.ProcessPayment;

public sealed class ProcessPaymentUseCase(
    IPaymentRepository payments,
    IReservationRepository reservations,
    ExpireReservationsUseCase expiry,
    IPaymentProvider provider,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<PaymentProcessingResult> ExecuteAsync(
        ProcessPaymentInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var payment = await payments.GetByIdempotencyKeyAsync(
            input.ChannelId, input.IdempotencyKey, cancellationToken);

        if (payment is not null && payment.ReservationCode != input.ReservationCode)
            return new(null, PaymentFailure.IdempotencyKeyReused);

        if (payment is null)
        {
            var startFailure = await StartPaymentAsync(input, nowUtc, cancellationToken);
            if (startFailure.Failure is not null)
                return new(null, startFailure.Failure);
            payment = startFailure.Payment!;
        }

        var currentReservation = await reservations.GetByCodeAsync(
            input.ReservationCode, input.ChannelId, cancellationToken);
        if (currentReservation is null)
            return new(null, PaymentFailure.ReservationNotFound);

        if (payment.Status != PaymentStatus.Pending)
            return new(PaymentDetails.From(payment, currentReservation.Status), IsReplay: true);

        // The provider call happens with no database transaction held open.
        var providerOutcome = await provider.ProcessAsync(payment, cancellationToken);
        var finalizedAtUtc = timeProvider.GetUtcNow();

        // Let the normal expiry path update the reservation and release its hold
        // before applying a late provider success.
        if (providerOutcome.Status == ProviderPaymentStatus.Succeeded)
        {
            await expiry.ExpireIfDueAsync(
                input.ReservationCode,
                input.ChannelId,
                finalizedAtUtc,
                cancellationToken);
        }

        // The expiry operation may have changed database state outside the
        // current identity map. Reload all entities in the final transaction.
        unitOfWork.ClearTracking();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var currentPayment = await payments.GetByIdAsync(payment.Id, cancellationToken);
                var reservation = await reservations.GetByCodeAsync(
                    input.ReservationCode, input.ChannelId, cancellationToken);

                if (currentPayment is null || reservation is null)
                    return new(null, PaymentFailure.ReservationNotFound);

                if (currentPayment.Status != PaymentStatus.Pending)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new(PaymentDetails.From(currentPayment, reservation.Status), IsReplay: true);
                }

                switch (providerOutcome.Status)
                {
                    case ProviderPaymentStatus.Pending:
                        currentPayment.RecordPendingProviderResult(
                            providerOutcome.ProviderReference, finalizedAtUtc);
                        break;
                    case ProviderPaymentStatus.Declined:
                        currentPayment.MarkDeclined(
                            providerOutcome.ProviderReference, finalizedAtUtc);
                        break;
                    case ProviderPaymentStatus.Succeeded:
                        currentPayment.MarkSucceeded(
                            providerOutcome.ProviderReference, finalizedAtUtc);
                        if (!reservation.TryConfirm(finalizedAtUtc))
                            currentPayment.MarkRefundRequired(finalizedAtUtc);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(providerOutcome));
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(PaymentDetails.From(currentPayment, reservation.Status));
            }
            catch (ConcurrencyConflictException) when (attempt == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                // The unit of work clears its stale tracked entities; reload and
                // reapply the provider result against the latest domain state.
            }
        }

        throw new InvalidOperationException("Could not finalize the payment after a concurrency retry.");
    }

    private async Task<StartPaymentOutcome> StartPaymentAsync(
        ProcessPaymentInput input,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await expiry.ExpireIfDueAsync(
            input.ReservationCode, input.ChannelId, nowUtc, cancellationToken);

        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            var existing = await payments.GetByIdempotencyKeyAsync(
                input.ChannelId, input.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                if (existing.ReservationCode != input.ReservationCode)
                    return new(null, PaymentFailure.IdempotencyKeyReused);

                await transaction.CommitAsync(cancellationToken);
                return new(existing);
            }

            var reservation = await reservations.GetByCodeAsync(
                input.ReservationCode, input.ChannelId, cancellationToken);
            if (reservation is null)
                return new(null, PaymentFailure.ReservationNotFound);

            if (!reservation.CanStartPaymentAt(nowUtc))
                return new(null, PaymentFailure.ReservationNotPayable);

            if (await payments.HasPendingForReservationAsync(
                    input.ReservationCode, cancellationToken))
                return new(null, PaymentFailure.PaymentAlreadyPending);

            var payment = PaymentAttempt.Create(
                input.ReservationCode,
                input.ChannelId,
                input.IdempotencyKey,
                nowUtc);
            payments.Add(payment);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(payment);
        }
        catch (PersistenceConflictException)
        {
            var existing = await payments.GetByIdempotencyKeyAsync(
                input.ChannelId, input.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                if (existing.ReservationCode != input.ReservationCode)
                    return new(null, PaymentFailure.IdempotencyKeyReused);
                return new(existing);
            }

            if (await payments.HasPendingForReservationAsync(
                    input.ReservationCode, cancellationToken))
                return new(null, PaymentFailure.PaymentAlreadyPending);

            throw;
        }
    }

    private sealed record StartPaymentOutcome(
        PaymentAttempt? Payment,
        PaymentFailure? Failure = null);
}
