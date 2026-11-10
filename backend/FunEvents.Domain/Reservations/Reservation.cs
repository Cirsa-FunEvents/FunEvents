namespace FunEvents.Domain.Reservations;

public sealed class Reservation
{
    private Reservation()
    {
    }

    public Guid Id { get; private set; }
    public string ReservationCode { get; private set; } = string.Empty;
    public string EventCode { get; private set; } = string.Empty;
    public string SessionCode { get; private set; } = string.Empty;
    public string UserCode { get; private set; } = string.Empty;
    public string ChannelId { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public static Reservation Create(
        string eventCode,
        string sessionCode,
        string userCode,
        string channelId,
        int quantity,
        DateTimeOffset createdAtUtc,
        TimeSpan holdDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(userCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (holdDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(holdDuration), "Hold duration must be positive.");

        var id = Guid.NewGuid();
        var createdAt = createdAtUtc.ToUniversalTime();
        return new Reservation
        {
            Id = id,
            ReservationCode = $"RSV-{id:N}".ToUpperInvariant(),
            EventCode = eventCode,
            SessionCode = sessionCode,
            UserCode = userCode,
            ChannelId = channelId,
            Quantity = quantity,
            Status = ReservationStatus.Pending,
            CreatedAtUtc = createdAt,
            ExpiresAtUtc = createdAt.Add(holdDuration)
        };
    }

    public bool Cancel(DateTimeOffset nowUtc)
    {
        if (Status != ReservationStatus.Pending)
            return false;

        Status = ExpiresAtUtc <= nowUtc.ToUniversalTime()
            ? ReservationStatus.Expired
            : ReservationStatus.Cancelled;
        return true;
    }

    public bool ExpireIfDue(DateTimeOffset nowUtc)
    {
        if (Status != ReservationStatus.Pending || ExpiresAtUtc > nowUtc.ToUniversalTime())
            return false;

        Status = ReservationStatus.Expired;
        return true;
    }

    public bool TryConfirm(DateTimeOffset nowUtc)
    {
        if (Status != ReservationStatus.Pending || ExpiresAtUtc <= nowUtc.ToUniversalTime())
            return false;

        Status = ReservationStatus.Confirmed;
        return true;
    }

    public bool CanStartPaymentAt(DateTimeOffset nowUtc) =>
        Status == ReservationStatus.Pending && ExpiresAtUtc > nowUtc.ToUniversalTime();
}
