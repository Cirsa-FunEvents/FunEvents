using FunEvents.Application.Repositories.Users;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Repositories.Users;

public sealed class UserRepository(FunEventsDbContext db) : IUserRepository
{
    public Task<bool> ExistsAsync(string userCode, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(x => x.UserCode == userCode, cancellationToken);
}
