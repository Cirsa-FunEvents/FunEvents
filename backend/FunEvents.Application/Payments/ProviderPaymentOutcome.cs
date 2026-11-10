namespace FunEvents.Application.Payments;

public enum ProviderPaymentStatus
{
    Pending,
    Succeeded,
    Declined
}

public sealed record ProviderPaymentOutcome(
    ProviderPaymentStatus Status,
    string ProviderReference);
