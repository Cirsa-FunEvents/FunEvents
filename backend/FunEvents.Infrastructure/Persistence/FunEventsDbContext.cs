using FunEvents.Domain.Reservations;
using FunEvents.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Infrastructure.Persistence;

public sealed class FunEventsDbContext(DbContextOptions<FunEventsDbContext> options) : DbContext(options)
{
    public DbSet<EventRecord> Events => Set<EventRecord>();
    public DbSet<EventSessionRecord> EventSessions => Set<EventSessionRecord>();
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<SessionInventoryRecord> SessionInventory => Set<SessionInventoryRecord>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventRecord>(entity =>
        {
            entity.ToTable("events");
            entity.HasKey(x => x.EventCode);
            entity.Property(x => x.EventCode).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<EventSessionRecord>(entity =>
        {
            entity.ToTable("event_sessions");
            entity.HasKey(x => new { x.EventCode, x.SessionCode });
            entity.Property(x => x.EventCode).HasMaxLength(64);
            entity.Property(x => x.SessionCode).HasMaxLength(64);
            entity.HasOne<EventRecord>().WithMany().HasForeignKey(x => x.EventCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserRecord>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.UserCode);
            entity.Property(x => x.UserCode).HasMaxLength(64);
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<SessionInventoryRecord>(entity =>
        {
            entity.ToTable("session_inventory", table =>
                table.HasCheckConstraint("ck_session_inventory_nonnegative", "\"AvailableQuantity\" >= 0"));
            entity.HasKey(x => new { x.EventCode, x.SessionCode });
            entity.Property(x => x.EventCode).HasMaxLength(64);
            entity.Property(x => x.SessionCode).HasMaxLength(64);
            entity.HasOne<EventSessionRecord>().WithMany()
                .HasForeignKey(x => new { x.EventCode, x.SessionCode })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("reservations", table =>
                table.HasCheckConstraint("ck_reservations_quantity_positive", "\"Quantity\" > 0"));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReservationCode).IsUnique();
            entity.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
            entity.Property(x => x.ReservationCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EventCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SessionCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ChannelId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsConcurrencyToken();
            entity.HasOne<EventSessionRecord>().WithMany()
                .HasForeignKey(x => new { x.EventCode, x.SessionCode })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.UserCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("reservation_idempotency");
            entity.HasKey(x => new { x.ChannelId, x.IdempotencyKey })
                .HasName("pk_reservation_idempotency");
            entity.Property(x => x.ChannelId).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReservationCode).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.ToTable("payment_attempts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PaymentCode).IsUnique();
            entity.HasIndex(x => new { x.ChannelId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.ReservationCode)
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending'")
                .HasDatabaseName("ux_payment_attempts_pending_reservation");
            entity.Property(x => x.PaymentCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReservationCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ChannelId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(24)
                .IsRequired()
                .IsConcurrencyToken();
            entity.Property(x => x.ProviderReference).HasMaxLength(100);
            entity.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationCode)
                .HasPrincipalKey(x => x.ReservationCode).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
