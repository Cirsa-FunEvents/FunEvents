using FunEvents.Api.Security.PartnerApiKey;
using FunEvents.Api.Reservations.Contracts;
using FunEvents.Api.Reservations.Validation;
using FunEvents.Application.Reservations.CancelReservation;
using FunEvents.Application.Reservations.CreateReservation;
using FunEvents.Application.Reservations.GetReservation;

namespace FunEvents.Api.Reservations;

public static class ReservationEndpoints
{
    public static RouteGroupBuilder MapReservationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/reservations")
            .WithTags("Reservations")
            .AddEndpointFilter<PartnerApiKeyEndpointFilter>();

        group.MapPost("", CreateAsync)
            .WithName("CreateReservation")
            .WithSummary("Create a temporary reservation hold");

        group.MapGet("/{reservationCode}", GetAsync)
            .WithName("GetReservation")
            .WithSummary("Get a reservation created by the authenticated channel");

        group.MapPost("/{reservationCode}/cancel", CancelAsync)
            .WithName("CancelReservation")
            .WithSummary("Cancel a pending reservation and release its inventory");

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateReservationRequest? request,
        HttpContext context,
        CreateReservationHandler handler,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var validationError = CreateReservationRequestValidator.Validate(request, idempotencyKey);
        if (validationError is not null)
            return ReservationHttpResultMapper.FromValidationError(validationError);

        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var validRequest = request!;
        var result = await handler.HandleAsync(
            new CreateReservationCommand(
                validRequest.EventCode.Trim(),
                validRequest.SessionCode.Trim(),
                validRequest.UserCode.Trim(),
                validRequest.Quantity,
                channelId,
                idempotencyKey!),
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (result.Reservation is { } reservation)
        {
            context.Response.Headers.Location = $"/api/v1/reservations/{reservation.ReservationCode}";
            if (result.IsReplay)
                context.Response.Headers["Idempotency-Replayed"] = "true";
        }

        return ReservationHttpResultMapper.FromCreateResult(result);
    }

    private static async Task<IResult> GetAsync(
        string reservationCode,
        HttpContext context,
        GetReservationHandler handler,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var reservation = await handler.HandleAsync(
            new GetReservationQuery(reservationCode, channelId),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return ReservationHttpResultMapper.FromReservation(reservation);
    }

    private static async Task<IResult> CancelAsync(
        string reservationCode,
        HttpContext context,
        CancelReservationHandler handler,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var reservation = await handler.HandleAsync(
            new CancelReservationCommand(reservationCode, channelId),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return ReservationHttpResultMapper.FromReservation(reservation);
    }
}
