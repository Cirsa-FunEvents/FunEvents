using FunEvents.Application.Common.Persistence;
using FunEvents.Domain.Reservations;
using Moq;

namespace FunEvents.Application.Tests.TestDoubles;

internal static class TestSupport
{
    public static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public static Reservation CreatePendingReservation(
        DateTimeOffset? createdAt = null,
        TimeSpan? holdDuration = null,
        string eventCode = "EVT-001") =>
        Reservation.Create(
            eventCode,
            "SES-001-A",
            "USR-042",
            "console",
            2,
            createdAt ?? Now,
            holdDuration ?? TimeSpan.FromMinutes(10));

    public static (Mock<IUnitOfWork> UnitOfWork, Mock<IUnitOfWorkTransaction> Transaction)
        CreateUnitOfWork()
    {
        var transaction = new Mock<IUnitOfWorkTransaction>();
        transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transaction.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return (unitOfWork, transaction);
    }
}
