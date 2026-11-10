using FunEvents.Application.Payments;
using FunEvents.Domain.Payments;

namespace FunEvents.Infrastructure.Payments;

public sealed class FakePaymentProvider : IPaymentProvider
{
    public Task<ProviderPaymentOutcome> ProcessAsync(
        PaymentAttempt payment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = Random.Shared.Next(3) switch
        {
            0 => ProviderPaymentStatus.Pending,
            1 => ProviderPaymentStatus.Succeeded,
            _ => ProviderPaymentStatus.Declined
        };

        return Task.FromResult(new ProviderPaymentOutcome(
            status,
            $"FAKE-{Guid.NewGuid():N}".ToUpperInvariant()));
    }
}
