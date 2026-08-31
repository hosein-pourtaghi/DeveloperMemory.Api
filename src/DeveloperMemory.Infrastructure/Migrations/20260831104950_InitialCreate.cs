using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeveloperMemory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessingRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptExperiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptExperiments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptProcessingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfileVersion = table.Column<int>(type: "integer", nullable: true),
                    Intent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TaskType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OptimizationMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Optimizer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OptimizerVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WasLlmUsed = table.Column<bool>(type: "boolean", nullable: false),
                    WasFallbackUsed = table.Column<bool>(type: "boolean", nullable: false),
                    TokenBudget = table.Column<int>(type: "integer", nullable: false),
                    EstimatedInputTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    QualityScore = table.Column<double>(type: "double precision", nullable: true),
                    ValidationStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessingDurationMs = table.Column<double>(type: "double precision", nullable: false),
                    ExperimentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VariantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MemoryIdsUsed = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkspaceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MemoryCount = table.Column<int>(type: "integer", nullable: false),
                    ConflictsDetected = table.Column<int>(type: "integer", nullable: false),
                    QualityGatePassed = table.Column<bool>(type: "boolean", nullable: false),
                    QualityGateFailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptProcessingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VectorEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Dimensions = table.Column<int>(type: "integer", nullable: false),
                    Vector = table.Column<float[]>(type: "real[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VectorEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    NormalizedContent = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MemoryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkspaceId = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Importance = table.Column<double>(type: "double precision", precision: 3, scale: 2, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", precision: 3, scale: 2, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryEntries_MemoryEntries_SupersededById",
                        column: x => x.SupersededById,
                        principalTable: "MemoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MemoryEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromptExperimentVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfileVersion = table.Column<int>(type: "integer", nullable: true),
                    OptimizationMode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptExperimentVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptExperimentVariants_PromptExperiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "PromptExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromptProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptProfileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptProfileVersions_PromptProfiles_PromptProfileId",
                        column: x => x.PromptProfileId,
                        principalTable: "PromptProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromptExperimentAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptExperimentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptExperimentAssignments_PromptExperimentVariants_Varian~",
                        column: x => x.VariantId,
                        principalTable: "PromptExperimentVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromptExperimentAssignments_PromptExperiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "PromptExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromptExperimentResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessingRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    QualityScore = table.Column<double>(type: "double precision", nullable: true),
                    QualityGatePassed = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedInputTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ProcessingDurationMs = table.Column<double>(type: "double precision", nullable: false),
                    WasFallbackUsed = table.Column<bool>(type: "boolean", nullable: false),
                    WasLlmUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptExperimentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptExperimentResults_PromptExperimentVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "PromptExperimentVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromptExperimentResults_PromptExperiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "PromptExperiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_AccessCount",
                table: "MemoryEntries",
                column: "AccessCount");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Classification",
                table: "MemoryEntries",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_CreatedAt",
                table: "MemoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_ExpiresAt",
                table: "MemoryEntries",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_MemoryType",
                table: "MemoryEntries",
                column: "MemoryType");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_ProjectId",
                table: "MemoryEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Scope",
                table: "MemoryEntries",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Scope_ProjectId_State",
                table: "MemoryEntries",
                columns: new[] { "Scope", "ProjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Scope_State_MemoryType",
                table: "MemoryEntries",
                columns: new[] { "Scope", "State", "MemoryType" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Scope_UserId_State",
                table: "MemoryEntries",
                columns: new[] { "Scope", "UserId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_State",
                table: "MemoryEntries",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_State_ExpiresAt",
                table: "MemoryEntries",
                columns: new[] { "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_SupersededById",
                table: "MemoryEntries",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_UpdatedAt",
                table: "MemoryEntries",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptAuditEvents_CorrelationId",
                table: "PromptAuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptAuditEvents_CreatedAt",
                table: "PromptAuditEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromptAuditEvents_EventType",
                table: "PromptAuditEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_PromptAuditEvents_ProcessingRecordId",
                table: "PromptAuditEvents",
                column: "ProcessingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentAssignments_ExperimentId",
                table: "PromptExperimentAssignments",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentAssignments_ExperimentId_AssignmentKeyHash",
                table: "PromptExperimentAssignments",
                columns: new[] { "ExperimentId", "AssignmentKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentAssignments_VariantId",
                table: "PromptExperimentAssignments",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentResults_CreatedAt",
                table: "PromptExperimentResults",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentResults_ExperimentId",
                table: "PromptExperimentResults",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentResults_ExperimentId_VariantId",
                table: "PromptExperimentResults",
                columns: new[] { "ExperimentId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentResults_VariantId",
                table: "PromptExperimentResults",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperiments_CreatedAt",
                table: "PromptExperiments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperiments_Status",
                table: "PromptExperiments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentVariants_ExperimentId",
                table: "PromptExperimentVariants",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptExperimentVariants_ExperimentId_Enabled",
                table: "PromptExperimentVariants",
                columns: new[] { "ExperimentId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptProcessingRecords_CorrelationId",
                table: "PromptProcessingRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProcessingRecords_CreatedAt",
                table: "PromptProcessingRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProcessingRecords_ExperimentId",
                table: "PromptProcessingRecords",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProcessingRecords_ProfileId",
                table: "PromptProcessingRecords",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProcessingRecords_UserId",
                table: "PromptProcessingRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProcessingRecords_ValidationStatus",
                table: "PromptProcessingRecords",
                column: "ValidationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProfiles_Enabled",
                table: "PromptProfiles",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProfiles_Name",
                table: "PromptProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptProfileVersions_CreatedAt",
                table: "PromptProfileVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProfileVersions_IsActive",
                table: "PromptProfileVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProfileVersions_PromptProfileId",
                table: "PromptProfileVersions",
                column: "PromptProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptProfileVersions_PromptProfileId_Version",
                table: "PromptProfileVersions",
                columns: new[] { "PromptProfileId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VectorEntries_CreatedAt",
                table: "VectorEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VectorEntries_MemoryId",
                table: "VectorEntries",
                column: "MemoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VectorEntries_Provider_Model",
                table: "VectorEntries",
                columns: new[] { "Provider", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_VectorEntries_Provider_Model_Dimensions",
                table: "VectorEntries",
                columns: new[] { "Provider", "Model", "Dimensions" });

            migrationBuilder.CreateIndex(
                name: "IX_VectorEntries_UpdatedAt",
                table: "VectorEntries",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryEntries");

            migrationBuilder.DropTable(
                name: "PromptAuditEvents");

            migrationBuilder.DropTable(
                name: "PromptExperimentAssignments");

            migrationBuilder.DropTable(
                name: "PromptExperimentResults");

            migrationBuilder.DropTable(
                name: "PromptProcessingRecords");

            migrationBuilder.DropTable(
                name: "PromptProfileVersions");

            migrationBuilder.DropTable(
                name: "VectorEntries");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "PromptExperimentVariants");

            migrationBuilder.DropTable(
                name: "PromptProfiles");

            migrationBuilder.DropTable(
                name: "PromptExperiments");
        }
    }
}
