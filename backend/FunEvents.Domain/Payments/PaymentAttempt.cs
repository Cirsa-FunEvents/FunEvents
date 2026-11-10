namespace FunEvents.Domain.Payments;

public sealed class PaymentAttempt
{
    private PaymentAttempt()
    {
    }

    public Guid Id { get; private set; }
    public string PaymentCode { get; private set; } = string.Empty;
    public string ReservationCode { get; private set; } = string.Empty;
    public string ChannelId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? ProviderReference { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PaymentAttempt Create(
        string reservationCode,
        string channelId,
        string idempotencyKey,
        DateTimeOffset nowUtc)
    {
        var id = Guid.NewGuid();
        var now = nowUtc.ToUniversalTime();
        return new PaymentAttempt
        {
            Id = id,
            PaymentCode = $"PAY-{id:N}".ToUpperInvariant(),
            ReservationCode = reservationCode,
            ChannelId = channelId,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void MarkSucceeded(string providerReference, DateTimeOffset nowUtc)
    {
        EnsurePending();
        ProviderReference = providerReference;
        Status = PaymentStatus.Succeeded;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void RecordPendingProviderResult(string providerReference, DateTimeOffset nowUtc)
    {
        EnsurePending();
        ProviderReference = providerReference;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void MarkDeclined(string providerReference, DateTimeOffset nowUtc)
    {
        EnsurePending();
        ProviderReference = providerReference;
        Status = PaymentStatus.Declined;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void MarkRefundRequired(DateTimeOffset nowUtc)
    {
        if (Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException("Only a successful payment can require a refund.");

        Status = PaymentStatus.RefundRequired;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only a pending payment can be finalized.");
    }
}
