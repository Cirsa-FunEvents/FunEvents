using FunEvents.Application.Reservations.ExpireReservations;

namespace FunEvents.Worker;

public sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expiry = scope.ServiceProvider.GetRequiredService<ExpireReservationsUseCase>();
                var expired = await expiry.ExpireDueAsync(
                    DateTimeOffset.UtcNow, 100, stoppingToken);
                if (expired > 0)
                    logger.LogInformation("Expired {ReservationCount} reservations", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reservation expiry pass failed");
            }
        }
    }
}
