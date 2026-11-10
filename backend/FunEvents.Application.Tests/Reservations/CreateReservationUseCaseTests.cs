using System.Security.Cryptography;
using System.Text;
using FunEvents.Application.Reservations;
using FunEvents.Application.Reservations.CreateReservation;
using FunEvents.Application.Reservations.CreateReservation.Dtos;
using FunEvents.Application.Repositories.Reservations;
using FunEvents.Application.Repositories.SessionInventory;
using FunEvents.Application.Repositories.Users;
using FunEvents.Application.Tests.TestDoubles;
using FunEvents.Domain.Reservations;
using Moq;
using Xunit;

namespace FunEvents.Application.Tests.Reservations;

public sealed class CreateReservationUseCaseTests
{
    private static readonly CancellationToken CancellationToken = System.Threading.CancellationToken.None;

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task ExecuteAsync_WhenQuantityOutsidePolicy_ReturnsInvalidQuantity(int quantity)
    {
        var reservations = new Mock<IReservationRepository>();
        var catalog = new Mock<IReservationCatalog>();
        var users = new Mock<IUserRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        var useCase = new CreateReservationUseCase(
            reservations.Object,
            catalog.Object,
            users.Object,
            inventory.Object,
            unitOfWork.UnitOfWork.Object,
            new ReservationPolicy(TimeSpan.FromMinutes(10), 6));

        var result = await useCase.ExecuteAsync(Input(quantity), TestSupport.Now, CancellationToken);

        Assert.Equal(CreateReservationFailure.InvalidQuantity, result.Failure);
        Assert.Null(result.Reservation);
        unitOfWork.UnitOfWork.Verify(
            x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        inventory.Verify(
            x => x.TryHoldAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_CreatesPendingReservationAndStoresIdempotency()
    {
        var reservationRepository = new Mock<IReservationRepository>();
        var catalog = new Mock<IReservationCatalog>();
        var users = new Mock<IUserRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        Reservation? savedReservation = null;
        ReservationIdempotencyRecord? savedIdempotency = null;

        reservationRepository
            .Setup(x => x.GetIdempotencyRecordAsync("console", "reservation-key", CancellationToken))
            .ReturnsAsync((ReservationIdempotencyRecord?)null);
        reservationRepository.Setup(x => x.Add(It.IsAny<Reservation>()))
            .Callback<Reservation>(reservation => savedReservation = reservation);
        reservationRepository.Setup(x => x.AddIdempotencyRecord(It.IsAny<ReservationIdempotencyRecord>()))
            .Callback<ReservationIdempotencyRecord>(record => savedIdempotency = record);
        catalog.Setup(x => x.IsActiveEventAsync("EVT-001", CancellationToken)).ReturnsAsync(true);
        catalog.Setup(x => x.GetSessionAsync("EVT-001", "SES-001-A", CancellationToken))
            .ReturnsAsync(new ReservationSession(true, TestSupport.Now.AddDays(2)));
        users.Setup(x => x.ExistsAsync("USR-042", CancellationToken)).ReturnsAsync(true);
        inventory.Setup(x => x.TryHoldAsync("EVT-001", "SES-001-A", 2, CancellationToken))
            .ReturnsAsync(true);

        var useCase = new CreateReservationUseCase(
            reservationRepository.Object,
            catalog.Object,
            users.Object,
            inventory.Object,
            unitOfWork.UnitOfWork.Object,
            new ReservationPolicy(TimeSpan.FromMinutes(10), 6));

        var result = await useCase.ExecuteAsync(Input(2), TestSupport.Now, CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.IsReplay);
        Assert.Equal(nameof(ReservationStatus.Pending), result.Reservation!.Status);
        Assert.Equal(TestSupport.Now.AddMinutes(10), result.Reservation.ExpiresAt);
        Assert.NotNull(savedReservation);
        Assert.NotNull(savedIdempotency);
        Assert.Equal(savedReservation!.ReservationCode, savedIdempotency!.ReservationCode);
        Assert.Equal("reservation-key", savedIdempotency.IdempotencyKey);
        inventory.Verify(
            x => x.TryHoldAsync("EVT-001", "SES-001-A", 2, CancellationToken), Times.Once);
        unitOfWork.UnitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken), Times.Once);
        unitOfWork.Transaction.Verify(x => x.CommitAsync(CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSameIdempotencyRequestWasAlreadyStored_ReturnsReplayWithoutHoldingInventory()
    {
        var reservation = TestSupport.CreatePendingReservation();
        var request = Input(2);
        var reservationRepository = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var catalog = new Mock<IReservationCatalog>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        reservationRepository
            .Setup(x => x.GetIdempotencyRecordAsync("console", "reservation-key", CancellationToken))
            .ReturnsAsync(new ReservationIdempotencyRecord(
                "console", "reservation-key", Fingerprint(request), reservation.ReservationCode, TestSupport.Now));
        reservationRepository.Setup(x => x.GetByCodeAsync(
                reservation.ReservationCode, "console", CancellationToken))
            .ReturnsAsync(reservation);

        var useCase = new CreateReservationUseCase(
            reservationRepository.Object,
            catalog.Object,
            users.Object,
            inventory.Object,
            unitOfWork.UnitOfWork.Object,
            new ReservationPolicy(TimeSpan.FromMinutes(10), 6));

        var result = await useCase.ExecuteAsync(request, TestSupport.Now, CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.IsReplay);
        Assert.Equal(reservation.ReservationCode, result.Reservation!.ReservationCode);
        catalog.Verify(x => x.IsActiveEventAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        inventory.Verify(
            x => x.TryHoldAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyHasDifferentRequest_ReturnsConflictWithoutCreatingReservation()
    {
        var reservationRepository = new Mock<IReservationRepository>();
        var inventory = new Mock<ISessionInventoryRepository>();
        var unitOfWork = TestSupport.CreateUnitOfWork();
        reservationRepository
            .Setup(x => x.GetIdempotencyRecordAsync("console", "reservation-key", CancellationToken))
            .ReturnsAsync(new ReservationIdempotencyRecord(
                "console", "reservation-key", "different-fingerprint", "RSV-EXISTING", TestSupport.Now));

        var useCase = new CreateReservationUseCase(
            reservationRepository.Object,
            Mock.Of<IReservationCatalog>(),
            Mock.Of<IUserRepository>(),
            inventory.Object,
            unitOfWork.UnitOfWork.Object,
            new ReservationPolicy(TimeSpan.FromMinutes(10), 6));

        var result = await useCase.ExecuteAsync(Input(2), TestSupport.Now, CancellationToken);

        Assert.Equal(CreateReservationFailure.IdempotencyKeyReused, result.Failure);
        Assert.Null(result.Reservation);
        inventory.Verify(
            x => x.TryHoldAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CreateReservationInput Input(int quantity) => new(
        "EVT-001", "SES-001-A", "USR-042", quantity, "console", "reservation-key");

    private static string Fingerprint(CreateReservationInput input)
    {
        var request = $"{input.EventCode}\n{input.SessionCode}\n{input.UserCode}\n{input.Quantity}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
    }
}
