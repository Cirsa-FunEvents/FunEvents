using FunEvents.Application.Repositories.Reservations;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Repositories.Reservations;

public sealed class ReservationCatalog(FunEventsDbContext db) : IReservationCatalog
{
    public Task<bool> IsActiveEventAsync(string eventCode, CancellationToken cancellationToken) =>
        db.Events.AnyAsync(x => x.EventCode == eventCode && x.IsActive, cancellationToken);

    public async Task<ReservationSession?> GetSessionAsync(
        string eventCode,
        string sessionCode,
        CancellationToken cancellationToken)
    {
        var session = await db.EventSessions.AsNoTracking().SingleOrDefaultAsync(
            x => x.EventCode == eventCode && x.SessionCode == sessionCode,
            cancellationToken);
        return session is null ? null : new(session.IsSalesOpen, session.StartsAtUtc);
    }
}
