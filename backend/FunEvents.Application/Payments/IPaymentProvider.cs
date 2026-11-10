using FunEvents.Domain.Payments;

namespace FunEvents.Application.Payments;

public interface IPaymentProvider
{
    Task<ProviderPaymentOutcome> ProcessAsync(
        PaymentAttempt payment,
        CancellationToken cancellationToken);
}
