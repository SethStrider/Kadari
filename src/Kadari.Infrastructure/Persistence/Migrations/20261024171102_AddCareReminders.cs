using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadari.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareReminders",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SnoozedUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredVia = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareReminders_AssetId",
                schema: "kadari",
                table: "CareReminders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CareReminders_DedupeKey",
                schema: "kadari",
                table: "CareReminders",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareReminders_Status_DueOn",
                schema: "kadari",
                table: "CareReminders",
                columns: new[] { "Status", "DueOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareReminders",
                schema: "kadari");
        }
    }
}
