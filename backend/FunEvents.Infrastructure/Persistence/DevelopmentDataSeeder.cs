using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(FunEventsDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Events.AnyAsync(x => x.EventCode == "EVT-001", cancellationToken))
        {
            db.Events.Add(new EventRecord
            {
                EventCode = "EVT-001",
                Name = "FunEvents Demo Concert",
                IsActive = true
            });
        }

        if (!await db.Users.AnyAsync(x => x.UserCode == "USR-042", cancellationToken))
        {
            db.Users.Add(new UserRecord
            {
                UserCode = "USR-042",
                DisplayName = "Demo Buyer"
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!await db.EventSessions.AnyAsync(
                x => x.EventCode == "EVT-001" && x.SessionCode == "SES-001-A",
                cancellationToken))
        {
            db.EventSessions.Add(new EventSessionRecord
            {
                EventCode = "EVT-001",
                SessionCode = "SES-001-A",
                StartsAtUtc = DateTimeOffset.UtcNow.AddDays(30),
                IsSalesOpen = true
            });
            db.SessionInventory.Add(new SessionInventoryRecord
            {
                EventCode = "EVT-001",
                SessionCode = "SES-001-A",
                AvailableQuantity = 100
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
