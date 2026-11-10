namespace FunEvents.Application.Common.Persistence;

public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The requested state change conflicted with another operation.", innerException)
{
}
