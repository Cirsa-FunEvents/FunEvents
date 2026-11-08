using FunEvents.Api.Reservations.Contracts;

namespace FunEvents.Api.Reservations.Validation;

public sealed record ReservationRequestValidationError(string Code, string Detail);

public static class CreateReservationRequestValidator
{
    public static ReservationRequestValidationError? Validate(
        CreateReservationRequest? request,
        string? idempotencyKey)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.EventCode)
            || string.IsNullOrWhiteSpace(request.SessionCode)
            || string.IsNullOrWhiteSpace(request.UserCode))
        {
            return new(
                "invalid_request",
                "Event, session, and user codes are required.");
        }

        if (request.EventCode.Trim().Length > 64
            || request.SessionCode.Trim().Length > 64
            || request.UserCode.Trim().Length > 64)
        {
            return new(
                "invalid_request",
                "Event, session, and user codes must not exceed 64 characters.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
        {
            return new(
                "invalid_idempotency_key",
                "Idempotency-Key is required and must not exceed 200 characters.");
        }

        return null;
    }
}
