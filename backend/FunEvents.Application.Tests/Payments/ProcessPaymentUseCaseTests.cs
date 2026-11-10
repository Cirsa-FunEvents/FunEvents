using FunEvents.Application.Payments;
using FunEvents.Application.Payments.ProcessPayment;
using FunEvents.Application.Payments.ProcessPayment.Dtos;
using FunEvents.Application.Repositories.Payments;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.SessionInventory;
using FunEvents.Application.Reservations.ExpireReservations;
using FunEvents.Application.Tests.TestDoubles;
using FunEvents.Domain.Payments;
using FunEvents.Domain.Reservations;
using Moq;
using Xunit;

namespace FunEvents.Application.Tests.Payments;

public sealed class ProcessPaymentUseCaseTests
{
    private static readonly CancellationToken CancellationToken = System.Threading.CancellationToken.None;

    [Fact]
    public async Task ExecuteAsync_WhenProviderSucceeds_ConfirmsReservationAndSavesPayment()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var payments = new Mock<IPaymentRepository>();
        var reservations = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var provider = new Mock<IPaymentProvider>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var clock = new Mock<TimeProvider>();
        PaymentAttempt? createdPayment = null;
        var finalizedAt = TestSupport.Now.AddMinutes(1);

        payments.Setup(x => x.GetByIdempotencyKeyAsync("console", "payment-key", CancellationToken))
            .ReturnsAsync((PaymentAttempt?)null);
        payments.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .Returns((Guid _, CancellationToken _) => Task.FromResult(createdPayment));
        payments.Setup(x => x.HasPendingForReservationAsync(reservation.ReservationCode, CancellationToken))
            .ReturnsAsync(false);
        payments.Setup(x => x.Add(It.IsAny<PaymentAttempt>()))
            .Callback<PaymentAttempt>(payment => createdPayment = payment);
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);
        provider.Setup(x => x.ProcessAsync(It.IsAny<PaymentAttempt>(), CancellationToken))
            .ReturnsAsync(new ProviderPaymentOutcome(ProviderPaymentStatus.Succeeded, "provider-ref-123"));
        clock.Setup(x => x.GetUtcNow()).Returns(finalizedAt);

        var expiry = new ExpireReservationsUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);
        var useCase = new ProcessPaymentUseCase(
            payments.Object,
            reservations.Object,
            expiry,
            provider.Object,
            unitOfWork.UnitOfWork.Object,
            clock.Object);

        var result = await useCase.ExecuteAsync(
            new ProcessPaymentInput(reservation.ReservationCode, "console", "payment-key"),
            TestSupport.Now,
            CancellationToken);

        Assert.Null(result.Failure);
        Assert.Equal(PaymentStatus.Succeeded, result.Payment!.PaymentStatus);
        Assert.Equal(ReservationStatus.Confirmed, result.Payment.ReservationStatus);
        Assert.Equal("provider-ref-123", result.Payment.ProviderReference);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.NotNull(createdPayment);
        payments.Verify(x => x.Add(It.IsAny<PaymentAttempt>()), Times.Once);
        unitOfWork.UnitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken), Times.Exactly(2));
        inventory.Verify(
            x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationHoldHasExpired_ReturnsNotPayableWithoutCallingProvider()
    {
        var reservation = TestSupport.CreatePendingReservation(
            holdDuration: TimeSpan.FromMinutes(1));
        var payments = new Mock<IPaymentRepository>();
        var reservations = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var provider = new Mock<IPaymentProvider>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var requestTime = TestSupport.Now.AddMinutes(2);

        payments.Setup(x => x.GetByIdempotencyKeyAsync("console", "payment-key", CancellationToken))
            .ReturnsAsync((PaymentAttempt?)null);
        reservations.Setup(x => x.GetByCodeAsync(reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);

        var expiry = new ExpireReservationsUseCase(
            reservations.Object, inventory.Object, unitOfWork.UnitOfWork.Object);
        var useCase = new ProcessPaymentUseCase(
            payments.Object,
            reservations.Object,
            expiry,
            provider.Object,
            unitOfWork.UnitOfWork.Object,
            TimeProvider.System);

        var result = await useCase.ExecuteAsync(
            new ProcessPaymentInput(reservation.ReservationCode, "console", "payment-key"),
            requestTime,
            CancellationToken);

        Assert.Equal(PaymentFailure.ReservationNotPayable, result.Failure);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        provider.Verify(
            x => x.ProcessAsync(It.IsAny<PaymentAttempt>(), It.IsAny<CancellationToken>()),
            Times.Never);
        payments.Verify(x => x.Add(It.IsAny<PaymentAttempt>()), Times.Never);
        inventory.Verify(x => x.ReleaseAsync("EVT-001", "SES-001-A", 2, CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyBelongsToAnotherReservation_ReturnsConflict()
    {
        var existingPayment = PaymentAttempt.Create(
            "RSV-OTHER", "console", "payment-key", TestSupport.Now);
        var payments = new Mock<IPaymentRepository>();
        var provider = new Mock<IPaymentProvider>();
        payments.Setup(x => x.GetByIdempotencyKeyAsync("console", "payment-key", CancellationToken))
            .ReturnsAsync(existingPayment);
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var expiry = new ExpireReservationsUseCase(
            Mock.Of<IReservationRepository>(),
            Mock.Of<ISessionInventoryRepository>(),
            unitOfWork.UnitOfWork.Object);
        var useCase = new ProcessPaymentUseCase(
            payments.Object,
            Mock.Of<IReservationRepository>(),
            expiry,
            provider.Object,
            unitOfWork.UnitOfWork.Object,
            TimeProvider.System);

        var result = await useCase.ExecuteAsync(
            new ProcessPaymentInput("RSV-REQUESTED", "console", "payment-key"),
            TestSupport.Now,
            CancellationToken);

        Assert.Equal(PaymentFailure.IdempotencyKeyReused, result.Failure);
        provider.Verify(
            x => x.ProcessAsync(It.IsAny<PaymentAttempt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
