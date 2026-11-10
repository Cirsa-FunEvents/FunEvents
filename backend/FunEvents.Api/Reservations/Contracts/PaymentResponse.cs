namespace FunEvents.Api.Reservations.Contracts;

public sealed record PaymentResponse(
    string PaymentCode,
    string ReservationCode,
    string PaymentStatus,
    string ReservationStatus,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
