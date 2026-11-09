using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Migrations.Seeding.Development;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(FunEventsDbContext db, CancellationToken cancellationToken)
    {
        var events = new[]
        {
            new EventRecord { EventCode = "EVT-001", Name = "FunEvents Demo Concert", IsActive = true },
            new EventRecord { EventCode = "EVT-002", Name = "The Midnight Players: Live Theatre", IsActive = true },
            new EventRecord { EventCode = "EVT-003", Name = "City Lights Comedy Night", IsActive = true },
            new EventRecord { EventCode = "EVT-004", Name = "Family Fun Festival", IsActive = true }
        };

        foreach (var eventRecord in events)
        {
            if (!await db.Events.AnyAsync(x => x.EventCode == eventRecord.EventCode, cancellationToken))
                db.Events.Add(eventRecord);
        }

        var users = new[]
        {
            new UserRecord { UserCode = "USR-042", DisplayName = "Demo Buyer" },
            new UserRecord { UserCode = "USR-043", DisplayName = "Alex Morgan" },
            new UserRecord { UserCode = "USR-100", DisplayName = "Sam Taylor" },
            new UserRecord { UserCode = "USR-200", DisplayName = "Jordan Lee" }
        };

        foreach (var user in users)
        {
            if (!await db.Users.AnyAsync(x => x.UserCode == user.UserCode, cancellationToken))
                db.Users.Add(user);
        }

        await db.SaveChangesAsync(cancellationToken);

        var nowUtc = DateTimeOffset.UtcNow;
        var sessions = new[]
        {
            new SessionSeed("EVT-001", "SES-001-A", nowUtc.AddDays(30), true, 100),
            new SessionSeed("EVT-001", "SES-001-B", nowUtc.AddDays(31), true, 80),
            new SessionSeed("EVT-002", "SES-002-A", nowUtc.AddDays(45), true, 60),
            new SessionSeed("EVT-002", "SES-002-B", nowUtc.AddDays(46), false, 60),
            new SessionSeed("EVT-003", "SES-003-A", nowUtc.AddDays(14), true, 40),
            new SessionSeed("EVT-003", "SES-003-B", nowUtc.AddDays(15), true, 40),
            new SessionSeed("EVT-004", "SES-004-A", nowUtc.AddDays(60), true, 150)
        };

        foreach (var session in sessions)
        {
            if (!await db.EventSessions.AnyAsync(
                    x => x.EventCode == session.EventCode && x.SessionCode == session.SessionCode,
                    cancellationToken))
            {
                db.EventSessions.Add(new EventSessionRecord
                {
                    EventCode = session.EventCode,
                    SessionCode = session.SessionCode,
                    StartsAtUtc = session.StartsAtUtc,
                    IsSalesOpen = session.IsSalesOpen
                });
            }

            if (!await db.SessionInventory.AnyAsync(
                    x => x.EventCode == session.EventCode && x.SessionCode == session.SessionCode,
                    cancellationToken))
            {
                db.SessionInventory.Add(new SessionInventoryRecord
                {
                    EventCode = session.EventCode,
                    SessionCode = session.SessionCode,
                    AvailableQuantity = session.AvailableQuantity
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SessionSeed(
        string EventCode,
        string SessionCode,
        DateTimeOffset StartsAtUtc,
        bool IsSalesOpen,
        int AvailableQuantity);
}
