namespace FunEvents.Application.Repositories.SessionInventory;

public interface ISessionInventoryRepository
{
    Task<bool> TryHoldAsync(
        string eventCode,
        string sessionCode,
        int quantity,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        string eventCode,
        string sessionCode,
        int quantity,
        CancellationToken cancellationToken);
}
