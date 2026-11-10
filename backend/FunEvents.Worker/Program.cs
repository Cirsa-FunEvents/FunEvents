using FunEvents.Infrastructure;
using FunEvents.Worker;
using FunEvents.Application.Reservations.ExpireReservations;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ExpireReservationsUseCase>();
builder.Services.AddHostedService<ReservationExpiryWorker>();

var host = builder.Build();
await host.RunAsync();
