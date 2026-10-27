using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadari.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelUsages",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CostMicros = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "char(3)", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelUsages_CustomerId",
                schema: "kadari",
                table: "ModelUsages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelUsages_OccurredAt_Feature",
                schema: "kadari",
                table: "ModelUsages",
                columns: new[] { "OccurredAt", "Feature" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelUsages",
                schema: "kadari");
        }
    }
}
