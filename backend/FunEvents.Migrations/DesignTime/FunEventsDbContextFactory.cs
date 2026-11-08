using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FunEvents.Migrations.DesignTime;

public sealed class FunEventsDbContextFactory : IDesignTimeDbContextFactory<FunEventsDbContext>
{
    public FunEventsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__funevents")
            ?? "Host=localhost;Database=funevents;Username=postgres";

        var options = new DbContextOptionsBuilder<FunEventsDbContext>()
            .UseNpgsql(connectionString, postgres =>
                postgres.MigrationsAssembly("FunEvents.Migrations"))
            .Options;

        return new FunEventsDbContext(options);
    }
}
