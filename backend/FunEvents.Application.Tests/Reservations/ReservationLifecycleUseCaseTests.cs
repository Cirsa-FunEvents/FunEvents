using FunEvents.Application.Common.Persistence;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.SessionInventory;
using FunEvents.Application.Reservations.CancelReservation;
using FunEvents.Application.Reservations.CancelReservation.Dtos;
using FunEvents.Application.Reservations.ExpireReservations;
using FunEvents.Application.Reservations.GetReservation;
using FunEvents.Application.Reservations.GetReservation.Dtos;
using FunEvents.Application.Tests.TestDoubles;
using FunEvents.Domain.Reservations;
using Moq;
using Xunit;

namespace FunEvents.Application.Tests.Reservations;

public sealed class ReservationLifecycleUseCaseTests
{
    private static readonly CancellationToken CancellationToken = System.Threading.CancellationToken.None;

    [Fact]
    public async Task CancelAsync_WhenReservationIsPending_ChangesStatusAndReleasesInventory()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var reservations = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);

        var useCase = new CancelReservationUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);

        var result = await useCase.ExecuteAsync(
            new CancelReservationInput(reservation.ReservationCode, "console"),
            TestSupport.Now.AddMinutes(1),
            CancellationToken);

        Assert.Equal(nameof(ReservationStatus.Cancelled), result.Reservation!.Status);
        inventory.Verify(x => x.ReleaseAsync("EVT-001", "SES-001-A", 2, CancellationToken), Times.Once);
        unitOfWork.UnitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken), Times.Once);
        unitOfWork.Transaction.Verify(x => x.CommitAsync(CancellationToken), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_WhenReservationIsAlreadyConfirmed_DoesNotReleaseInventory()
    {
        var reservation = TestSupport.CreatePendingReservation();
        Assert.True(reservation.TryConfirm(TestSupport.Now.AddMinutes(1)));
        var reservations = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);

        var useCase = new CancelReservationUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);

        var result = await useCase.ExecuteAsync(
            new CancelReservationInput(reservation.ReservationCode, "console"),
            TestSupport.Now.AddMinutes(2),
            CancellationToken);

        Assert.Equal(nameof(ReservationStatus.Confirmed), result.Reservation!.Status);
        inventory.Verify(
            x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWork.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpireIfDueAsync_WhenReservationIsExpired_ReleasesInventoryInTransaction()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var reservations = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);
        var useCase = new ExpireReservationsUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);

        var expired = await useCase.ExpireIfDueAsync(
            reservation.ReservationCode, "console", TestSupport.Now.AddMinutes(11), CancellationToken);

        Assert.True(expired);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        inventory.Verify(x => x.ReleaseAsync("EVT-001", "SES-001-A", 2, CancellationToken), Times.Once);
        unitOfWork.UnitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken), Times.Once);
        unitOfWork.Transaction.Verify(x => x.CommitAsync(CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ExpireIfDueAsync_WhenReservationIsNotDue_DoesNotSaveOrReleaseInventory()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var useCase = new ExpireReservationsUseCase(
            reservations.Object,
            inventory.Object,
            unitOfWork.UnitOfWork.Object);

        var expired = await useCase.ExpireIfDueAsync(
            reservation.ReservationCode, "console", TestSupport.Now.AddMinutes(1), CancellationToken);

        Assert.False(expired);
        unitOfWork.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        inventory.Verify(
            x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpireIfDueAsync_WhenConcurrentTransitionWins_DoesNotReleaseInventory()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        unitOfWork.UnitOfWork.Setup(x => x.SaveChangesAsync(CancellationToken))
            .ThrowsAsync(new ConcurrencyConflictException(new InvalidOperationException("Concurrent update")));
        var useCase = new ExpireReservationsUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);

        var expired = await useCase.ExpireIfDueAsync(
            reservation.ReservationCode, "console", TestSupport.Now.AddMinutes(11), CancellationToken);

        Assert.False(expired);
        unitOfWork.UnitOfWork.Verify(x => x.ClearTracking(), Times.Once);
        inventory.Verify(
            x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpireDueAsync_ExpiresDueCandidatesAndReturnsNumberExpired()
    {
        var first = TestSupport.CreatePendingReservation();
        var second = TestSupport.CreatePendingReservation(eventCode: "EVT-002");
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(x => x.GetDueForExpiryAsync(
                TestSupport.Now.AddMinutes(11), 100, CancellationToken))
            .ReturnsAsync(new[]
            {
                new ReservationExpiryCandidate(first.ReservationCode, "console"),
                new ReservationExpiryCandidate(second.ReservationCode, "console")
            });
        reservations.Setup(x => x.GetByCodeAsync(first.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(first);
        reservations.Setup(x => x.GetByCodeAsync(second.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(second);
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var useCase = new ExpireReservationsUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);

        var expiredCount = await useCase.ExpireDueAsync(
            TestSupport.Now.AddMinutes(11), 100, CancellationToken);

        Assert.Equal(2, expiredCount);
        Assert.Equal(ReservationStatus.Expired, first.Status);
        Assert.Equal(ReservationStatus.Expired, second.Status);
        inventory.Verify(x => x.ReleaseAsync("EVT-001", "SES-001-A", 2, CancellationToken), Times.Once);
        inventory.Verify(x => x.ReleaseAsync("EVT-002", "SES-001-A", 2, CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ExpiresDueReservationBeforeReturningIt()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var expiry = new ExpireReservationsUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);
        var useCase = new GetReservationUseCase(reservations.Object, expiry);

        var result = await useCase.ExecuteAsync(
            new GetReservationInput(reservation.ReservationCode, "console"),
            TestSupport.Now.AddMinutes(11),
            CancellationToken);

        Assert.Equal(nameof(ReservationStatus.Expired), result.Reservation!.Status);
        inventory.Verify(x => x.ReleaseAsync("EVT-001", "SES-001-A", 2, CancellationToken), Times.Once);
    }
}
