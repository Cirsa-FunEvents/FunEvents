namespace FunEvents.Application.Common.Persistence;

public sealed class PersistenceConflictException(Exception innerException)
    : Exception("The requested operation conflicted with an existing database record.", innerException)
{
}
