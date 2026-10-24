using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadari.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutingDecisions",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    MatchedRuleVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HumanOverride = table.Column<bool>(type: "boolean", nullable: false),
                    HumanOverrideReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutingRuleVersions",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "char(2)", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ModelCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RetailerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Coverage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IssueClass = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RouteType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RoutingModelCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    TargetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ChannelValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiredEvidence = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceEvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingDecisions_AssetId_EvaluatedAt",
                schema: "kadari",
                table: "RoutingDecisions",
                columns: new[] { "AssetId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingRuleVersions_CountryCode_Status_BrandId",
                schema: "kadari",
                table: "RoutingRuleVersions",
                columns: new[] { "CountryCode", "Status", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingRuleVersions_LogicalRuleId_Version",
                schema: "kadari",
                table: "RoutingRuleVersions",
                columns: new[] { "LogicalRuleId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutingDecisions",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "RoutingRuleVersions",
                schema: "kadari");
        }
    }
}
