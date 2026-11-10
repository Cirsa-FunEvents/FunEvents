using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.Users;
using FunEvents.Application.Common.Persistence;
using FunEvents.Application.Payments;
using FunEvents.Application.Repositories.Payments;
using FunEvents.Application.Repositories.SessionInventory;
using FunEvents.Infrastructure.Persistence;
using FunEvents.Infrastructure.Repositories.Reservations;
using FunEvents.Infrastructure.Repositories.Users;
using FunEvents.Infrastructure.Repositories.Payments;
using FunEvents.Infrastructure.Repositories.SessionInventory;
using FunEvents.Infrastructure.Payments;
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
        services.AddScoped<IReservationCatalog, ReservationCatalog>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionInventoryRepository, SessionInventoryRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
        return services;
    }
}
