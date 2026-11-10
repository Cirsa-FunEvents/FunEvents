using FunEvents.Api.Reservations.Contracts;
using FunEvents.Api.Reservations.Validation;
using FunEvents.Application.Reservations.CreateReservation.Dtos;
using FunEvents.Application.Reservations.Dtos;
using FunEvents.Application.Payments.ProcessPayment.Dtos;

namespace FunEvents.Api.Reservations;

public static class ReservationHttpResultMapper
{
    public static IResult FromValidationError(ReservationRequestValidationError error) =>
        Problem(StatusCodes.Status400BadRequest, error.Code, error.Detail);

    public static IResult FromCreateResult(CreateReservationResult result)
    {
        if (result.Failure is not null)
            return FromCreateFailure(result.Failure.Value);

        var reservation = result.Reservation!;
        var response = ReservationResponse.From(reservation);
        var location = $"/api/v1/reservations/{reservation.ReservationCode}";

        return result.IsReplay
            ? Results.Ok(response)
            : Results.Created(location, response);
    }

    public static IResult FromReservation(ReservationDetails? reservation) => reservation is null
        ? Problem(
            StatusCodes.Status404NotFound,
            "reservation_not_found",
            "The reservation was not found.")
        : Results.Ok(ReservationResponse.From(reservation));

    public static IResult FromPaymentResult(PaymentProcessingResult result)
    {
        if (result.Failure is { } failure)
        {
            var (status, code, detail) = failure switch
            {
                PaymentFailure.ReservationNotFound =>
                    (StatusCodes.Status404NotFound, "reservation_not_found", "The reservation was not found."),
                PaymentFailure.ReservationNotPayable =>
                    (StatusCodes.Status409Conflict, "reservation_not_payable", "The reservation is no longer payable."),
                PaymentFailure.PaymentAlreadyPending =>
                    (StatusCodes.Status409Conflict, "payment_already_pending", "A payment attempt is already pending for this reservation."),
                PaymentFailure.IdempotencyKeyReused =>
                    (StatusCodes.Status409Conflict, "idempotency_key_reused", "The idempotency key was already used for another payment attempt."),
                _ => (StatusCodes.Status500InternalServerError, "payment_failed", "The payment could not be processed.")
            };
            return Problem(status, code, detail);
        }

        var payment = result.Payment!;
        return Results.Ok(new PaymentResponse(
            payment.PaymentCode,
            payment.ReservationCode,
            payment.PaymentStatus.ToString(),
            payment.ReservationStatus.ToString(),
            payment.ProviderReference,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc));
    }

    private static IResult FromCreateFailure(CreateReservationFailure failure) => failure switch
    {
        CreateReservationFailure.InvalidQuantity =>
            Problem(StatusCodes.Status400BadRequest, "invalid_quantity", "Quantity must be between 1 and the configured maximum."),
        CreateReservationFailure.EventNotFound =>
            Problem(StatusCodes.Status404NotFound, "event_not_found", "The event was not found or is not active."),
        CreateReservationFailure.SessionNotFound =>
            Problem(StatusCodes.Status404NotFound, "session_not_found", "The session does not belong to the requested event."),
        CreateReservationFailure.UserNotFound =>
            Problem(StatusCodes.Status404NotFound, "user_not_found", "The user was not found."),
        CreateReservationFailure.SessionUnavailable =>
            Problem(StatusCodes.Status409Conflict, "session_unavailable", "Reservations are not open for this session."),
        CreateReservationFailure.InsufficientInventory =>
            Problem(StatusCodes.Status409Conflict, "insufficient_inventory", "There are not enough tickets available."),
        CreateReservationFailure.IdempotencyKeyReused =>
            Problem(StatusCodes.Status409Conflict, "idempotency_key_reused", "The idempotency key was already used with different request data."),
        _ => Problem(StatusCodes.Status500InternalServerError, "reservation_failed", "The reservation could not be created.")
    };

    private static IResult Problem(int status, string code, string detail) => Results.Problem(
        statusCode: status,
        title: code.Replace('_', ' '),
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
