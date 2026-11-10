namespace FunEvents.Application.Repositories.Reservations;

public interface IReservationCatalog
{
    Task<bool> IsActiveEventAsync(string eventCode, CancellationToken cancellationToken);
    Task<ReservationSession?> GetSessionAsync(
        string eventCode,
        string sessionCode,
        CancellationToken cancellationToken);
}

public sealed record ReservationSession(bool IsSalesOpen, DateTimeOffset StartsAtUtc);
