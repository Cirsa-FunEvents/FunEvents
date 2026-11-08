namespace FunEvents.Infrastructure.Persistence;

public sealed class EventRecord
{
    public string EventCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class EventSessionRecord
{
    public string EventCode { get; set; } = string.Empty;
    public string SessionCode { get; set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; set; }
    public bool IsSalesOpen { get; set; }
}

public sealed class UserRecord
{
    public string UserCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class SessionInventoryRecord
{
    public string EventCode { get; set; } = string.Empty;
    public string SessionCode { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
}

public sealed class IdempotencyRecord
{
    public string ChannelId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string ReservationCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
