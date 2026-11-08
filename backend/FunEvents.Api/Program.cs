using FunEvents.Api.Reservations;
using FunEvents.Api.Security.PartnerApiKey;
using FunEvents.Application.Reservations;
using FunEvents.Application.Reservations.CancelReservation;
using FunEvents.Application.Reservations.CreateReservation;
using FunEvents.Application.Reservations.GetReservation;
using FunEvents.Infrastructure;
using FunEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.Configure<PartnerApiKeyOptions>(
    builder.Configuration.GetSection("PartnerApiKeys"));
builder.Services.AddScoped<PartnerApiKeyEndpointFilter>();
builder.Services.AddScoped<CreateReservationHandler>();
builder.Services.AddScoped<GetReservationHandler>();
builder.Services.AddScoped<CancelReservationHandler>();
builder.Services.AddSingleton(TimeProvider.System);

var holdDuration = builder.Configuration.GetValue("ReservationPolicy:HoldDuration", TimeSpan.FromMinutes(10));
var maxQuantity = builder.Configuration.GetValue("ReservationPolicy:MaxQuantityPerReservation", 6);
if (holdDuration <= TimeSpan.Zero || maxQuantity <= 0)
    throw new InvalidOperationException("Reservation policy values must be positive.");

builder.Services.AddSingleton(new ReservationPolicy(holdDuration, maxQuantity));
var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<FunEventsDbContext>();
    await db.Database.MigrateAsync();
    await DevelopmentDataSeeder.SeedAsync(db, CancellationToken.None);
}

app.MapHealthChecks("/health");
app.MapReservationEndpoints();

app.Run();
