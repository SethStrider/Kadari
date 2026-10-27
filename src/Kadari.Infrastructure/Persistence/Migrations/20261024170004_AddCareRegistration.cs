using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadari.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverageRegistrations",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Destination = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RegisteredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalActionAudits",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuthorisationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Actor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Destination = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Step = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SharedSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Result = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalActionAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalActionAuthorisations",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeJson = table.Column<string>(type: "jsonb", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalActionAuthorisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationOpportunities",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BenefitSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DeadlineOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SurfacedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationOpportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationRuleVersions",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "char(2)", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ModelCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DestinationType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Destination = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequiredFields = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeadlineDays = table.Column<int>(type: "integer", nullable: true),
                    ExtensionMonths = table.Column<int>(type: "integer", nullable: true),
                    BenefitDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_RegistrationRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegistrations_AssetId",
                schema: "kadari",
                table: "CoverageRegistrations",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRegistrations_IdempotencyKey",
                schema: "kadari",
                table: "CoverageRegistrations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalActionAudits_AssetId_OccurredAt",
                schema: "kadari",
                table: "ExternalActionAudits",
                columns: new[] { "AssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalActionAuthorisations_AssetId",
                schema: "kadari",
                table: "ExternalActionAuthorisations",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationOpportunities_AssetId_LogicalRuleId",
                schema: "kadari",
                table: "RegistrationOpportunities",
                columns: new[] { "AssetId", "LogicalRuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationOpportunities_Status_DeadlineOn",
                schema: "kadari",
                table: "RegistrationOpportunities",
                columns: new[] { "Status", "DeadlineOn" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRuleVersions_CountryCode_Status_BrandId",
                schema: "kadari",
                table: "RegistrationRuleVersions",
                columns: new[] { "CountryCode", "Status", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRuleVersions_LogicalRuleId_Version",
                schema: "kadari",
                table: "RegistrationRuleVersions",
                columns: new[] { "LogicalRuleId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverageRegistrations",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "ExternalActionAudits",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "ExternalActionAuthorisations",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "RegistrationOpportunities",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "RegistrationRuleVersions",
                schema: "kadari");
        }
    }
}
