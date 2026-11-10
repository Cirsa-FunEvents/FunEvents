using FunEvents.Api.Security.PartnerApiKey;
using FunEvents.Api.Reservations.Contracts;
using FunEvents.Api.Reservations.Validation;
using FunEvents.Application.Reservations.CancelReservation;
using FunEvents.Application.Reservations.CancelReservation.Dtos;
using FunEvents.Application.Reservations.CreateReservation;
using FunEvents.Application.Reservations.CreateReservation.Dtos;
using FunEvents.Application.Reservations.GetReservation;
using FunEvents.Application.Reservations.GetReservation.Dtos;
using FunEvents.Application.Payments.ProcessPayment;
using FunEvents.Application.Payments.ProcessPayment.Dtos;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

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

        group.MapPost("/{reservationCode}/payments", ProcessPaymentAsync)
            .WithName("ProcessReservationPayment")
            .WithSummary("Run a simulated payment for a pending reservation");

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateReservationRequest? request,
        [FromHeader(Name = "Idempotency-Key")]
        [Description("Unique key for this reservation attempt. Reuse it when retrying the same request.")]
        string? idempotencyKey,
        HttpContext context,
        CreateReservationUseCase useCase,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validationError = CreateReservationRequestValidator.Validate(request, idempotencyKey);
        if (validationError is not null)
            return ReservationHttpResultMapper.FromValidationError(validationError);

        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var validRequest = request!;
        var result = await useCase.ExecuteAsync(
            new CreateReservationInput(
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
        GetReservationUseCase useCase,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var result = await useCase.ExecuteAsync(
            new GetReservationInput(reservationCode, channelId),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return ReservationHttpResultMapper.FromReservation(result.Reservation);
    }

    private static async Task<IResult> CancelAsync(
        string reservationCode,
        HttpContext context,
        CancelReservationUseCase useCase,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var result = await useCase.ExecuteAsync(
            new CancelReservationInput(reservationCode, channelId),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return ReservationHttpResultMapper.FromReservation(result.Reservation);
    }

    private static async Task<IResult> ProcessPaymentAsync(
        string reservationCode,
        [FromHeader(Name = "Idempotency-Key")]
        [Description("Unique key for this payment attempt. Reuse it when retrying the same attempt.")]
        string? idempotencyKey,
        HttpContext context,
        ProcessPaymentUseCase useCase,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            return ReservationHttpResultMapper.FromValidationError(new(
                "invalid_idempotency_key",
                "Provide an Idempotency-Key header with 1 to 200 characters."));

        var channelId = (string)context.Items[PartnerApiKeyEndpointFilter.ChannelIdItemKey]!;
        var result = await useCase.ExecuteAsync(
            new ProcessPaymentInput(reservationCode, channelId, idempotencyKey),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return ReservationHttpResultMapper.FromPaymentResult(result);
    }
}
