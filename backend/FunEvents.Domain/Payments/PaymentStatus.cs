namespace FunEvents.Domain.Payments;

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Declined,
    RefundRequired
}
