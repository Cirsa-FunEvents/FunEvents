using FunEvents.Application.Repositories.SessionInventory;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Repositories.SessionInventory;

public sealed class SessionInventoryRepository(FunEventsDbContext db) : ISessionInventoryRepository
{
    public async Task<bool> TryHoldAsync(
        string eventCode,
        string sessionCode,
        int quantity,
        CancellationToken cancellationToken)
    {
        var changed = await db.SessionInventory
            .Where(x => x.EventCode == eventCode
                && x.SessionCode == sessionCode
                && x.AvailableQuantity >= quantity)
            .ExecuteUpdateAsync(update => update.SetProperty(
                x => x.AvailableQuantity,
                x => x.AvailableQuantity - quantity), cancellationToken);
        return changed == 1;
    }

    public async Task ReleaseAsync(
        string eventCode,
        string sessionCode,
        int quantity,
        CancellationToken cancellationToken)
    {
        var changed = await db.SessionInventory
            .Where(x => x.EventCode == eventCode && x.SessionCode == sessionCode)
            .ExecuteUpdateAsync(update => update.SetProperty(
                x => x.AvailableQuantity,
                x => x.AvailableQuantity + quantity), cancellationToken);
        if (changed != 1)
            throw new InvalidOperationException("Inventory row for the reservation was not found.");
    }
}
