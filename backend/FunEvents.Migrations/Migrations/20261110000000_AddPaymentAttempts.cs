using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunEvents.Migrations.Migrations;

public partial class AddPaymentAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "payment_attempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ReservationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ChannelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                ProviderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_attempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_payment_attempts_reservations_ReservationCode",
                    column: x => x.ReservationCode,
                    principalTable: "reservations",
                    principalColumn: "ReservationCode",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_payment_attempts_PaymentCode",
            table: "payment_attempts",
            column: "PaymentCode",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_attempts_ChannelId_IdempotencyKey",
            table: "payment_attempts",
            columns: new[] { "ChannelId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_payment_attempts_pending_reservation",
            table: "payment_attempts",
            column: "ReservationCode",
            unique: true,
            filter: "\"Status\" = 'Pending'");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "payment_attempts");
}
