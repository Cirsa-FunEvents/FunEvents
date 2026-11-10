namespace FunEvents.Application.Repositories.Users;

public interface IUserRepository
{
    Task<bool> ExistsAsync(string userCode, CancellationToken cancellationToken);
}
