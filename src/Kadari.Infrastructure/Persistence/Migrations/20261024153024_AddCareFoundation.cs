using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadari.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PurchaseBrand",
                schema: "kadari",
                table: "AssetEvidences",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseModel",
                schema: "kadari",
                table: "AssetEvidences",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseRetailer",
                schema: "kadari",
                table: "AssetEvidences",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PurchasedOn",
                schema: "kadari",
                table: "AssetEvidences",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetHistoryEntries",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Brands",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ParentGroup = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LocalOperator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Categories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ServiceModel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RegistrationSignal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ResearchConfidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CareProfiles",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CoverageState = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CoverageExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    NextAttentionOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OpenCaseCount = table.Column<int>(type: "integer", nullable: false),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LatestDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverageDecisions",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultState = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    MatchedRuleVersionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverageRuleVersions",
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
                    CoverageType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: true),
                    StartBasis = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RegistrationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationDeadlineDays = table.Column<int>(type: "integer", nullable: true),
                    ExtensionMonths = table.Column<int>(type: "integer", nullable: true),
                    PlainLanguageSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceEvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersedesVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Retailers",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CountryCode = table.Column<string>(type: "char(2)", nullable: false),
                    GroupOwner = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetailFormat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Categories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntegrationStance = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    SourceEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Retailers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutingModels",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WhenItApplies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultAction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TypicalEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Examples = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceEvidences",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceEvidences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverageRecords",
                schema: "kadari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoverageType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistrationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PlainLanguageSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoverageRecords_CareProfiles_CareProfileId",
                        column: x => x.CareProfileId,
                        principalSchema: "kadari",
                        principalTable: "CareProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetHistoryEntries_AssetId_OccurredAt",
                schema: "kadari",
                table: "AssetHistoryEntries",
                columns: new[] { "AssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                schema: "kadari",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareProfiles_AssetId",
                schema: "kadari",
                table: "CareProfiles",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareProfiles_NextAttentionOn",
                schema: "kadari",
                table: "CareProfiles",
                column: "NextAttentionOn");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageDecisions_AssetId_EvaluatedAt",
                schema: "kadari",
                table: "CoverageDecisions",
                columns: new[] { "AssetId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRecords_CareProfileId_CoverageType",
                schema: "kadari",
                table: "CoverageRecords",
                columns: new[] { "CareProfileId", "CoverageType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRuleVersions_CountryCode_Status_BrandId",
                schema: "kadari",
                table: "CoverageRuleVersions",
                columns: new[] { "CountryCode", "Status", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRuleVersions_LogicalRuleId_Version",
                schema: "kadari",
                table: "CoverageRuleVersions",
                columns: new[] { "LogicalRuleId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Retailers_CountryCode_Slug",
                schema: "kadari",
                table: "Retailers",
                columns: new[] { "CountryCode", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingModels_Code",
                schema: "kadari",
                table: "RoutingModels",
                column: "Code",
                unique: true);

            // Every asset registered before the history existed gets its opening entry, dated when it was registered,
            // so no asset's history starts mid-story. Assets registered from now on get it from the domain event.
            migrationBuilder.Sql("""
                INSERT INTO kadari."AssetHistoryEntries" ("Id", "AssetId", "Kind", "Summary", "OccurredAt")
                SELECT gen_random_uuid(), a."Id", 'Registered', 'Added to KADARI.', a."RegisteredAt"
                FROM kadari."Assets" a
                WHERE NOT a."IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetHistoryEntries",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "Brands",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "CoverageDecisions",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "CoverageRecords",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "CoverageRuleVersions",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "Retailers",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "RoutingModels",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "SourceEvidences",
                schema: "kadari");

            migrationBuilder.DropTable(
                name: "CareProfiles",
                schema: "kadari");

            migrationBuilder.DropColumn(
                name: "PurchaseBrand",
                schema: "kadari",
                table: "AssetEvidences");

            migrationBuilder.DropColumn(
                name: "PurchaseModel",
                schema: "kadari",
                table: "AssetEvidences");

            migrationBuilder.DropColumn(
                name: "PurchaseRetailer",
                schema: "kadari",
                table: "AssetEvidences");

            migrationBuilder.DropColumn(
                name: "PurchasedOn",
                schema: "kadari",
                table: "AssetEvidences");
        }
    }
}
