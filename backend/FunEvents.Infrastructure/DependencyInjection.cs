using FunEvents.Application.Reservations;
using FunEvents.Infrastructure.Persistence;
using FunEvents.Infrastructure.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FunEvents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("funevents")
            ?? throw new InvalidOperationException("Connection string 'funevents' is not configured.");

        services.AddDbContext<FunEventsDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsAssembly("FunEvents.Migrations")));
        services.AddScoped<IReservationRepository, ReservationRepository>();
        return services;
    }
}
