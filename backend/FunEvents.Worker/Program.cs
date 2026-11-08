using FunEvents.Infrastructure;
using FunEvents.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReservationExpiryWorker>();

var host = builder.Build();
await host.RunAsync();
