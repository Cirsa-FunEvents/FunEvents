using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunEvents.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    EventCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.EventCode);
                });

            migrationBuilder.CreateTable(
                name: "reservation_idempotency",
                columns: table => new
                {
                    ChannelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReservationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservation_idempotency", x => new { x.ChannelId, x.IdempotencyKey });
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    UserCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.UserCode);
                });

            migrationBuilder.CreateTable(
                name: "event_sessions",
                columns: table => new
                {
                    EventCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSalesOpen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_sessions", x => new { x.EventCode, x.SessionCode });
                    table.ForeignKey(
                        name: "FK_event_sessions_events_EventCode",
                        column: x => x.EventCode,
                        principalTable: "events",
                        principalColumn: "EventCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.Id);
                    table.CheckConstraint("ck_reservations_quantity_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_reservations_event_sessions_EventCode_SessionCode",
                        columns: x => new { x.EventCode, x.SessionCode },
                        principalTable: "event_sessions",
                        principalColumns: new[] { "EventCode", "SessionCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_users_UserCode",
                        column: x => x.UserCode,
                        principalTable: "users",
                        principalColumn: "UserCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_inventory",
                columns: table => new
                {
                    EventCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AvailableQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_inventory", x => new { x.EventCode, x.SessionCode });
                    table.CheckConstraint("ck_session_inventory_nonnegative", "\"AvailableQuantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_session_inventory_event_sessions_EventCode_SessionCode",
                        columns: x => new { x.EventCode, x.SessionCode },
                        principalTable: "event_sessions",
                        principalColumns: new[] { "EventCode", "SessionCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_EventCode_SessionCode",
                table: "reservations",
                columns: new[] { "EventCode", "SessionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ReservationCode",
                table: "reservations",
                column: "ReservationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_Status_ExpiresAtUtc",
                table: "reservations",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_UserCode",
                table: "reservations",
                column: "UserCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_idempotency");

            migrationBuilder.DropTable(
                name: "reservations");

            migrationBuilder.DropTable(
                name: "session_inventory");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "event_sessions");

            migrationBuilder.DropTable(
                name: "events");
        }
    }
}
