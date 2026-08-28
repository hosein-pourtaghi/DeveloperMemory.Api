using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeveloperMemory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeysAndSecurityAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    ReplacedByKeyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    KeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MetadataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditLog", x => x.Id);
                });

            // ApiKeys indexes
            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OwnerId",
                table: "ApiKeys",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OwnerId_CreatedAt",
                table: "ApiKeys",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_ExpiresAt",
                table: "ApiKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_RevokedAt",
                table: "ApiKeys",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_LastUsedAt",
                table: "ApiKeys",
                column: "LastUsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_ReplacedByKeyId",
                table: "ApiKeys",
                column: "ReplacedByKeyId");

            // SecurityAuditLog indexes
            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLog_OccurredAt",
                table: "SecurityAuditLog",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLog_EventType",
                table: "SecurityAuditLog",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLog_OwnerId",
                table: "SecurityAuditLog",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLog_EventType_OccurredAt",
                table: "SecurityAuditLog",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLog_OwnerId_OccurredAt",
                table: "SecurityAuditLog",
                columns: new[] { "OwnerId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApiKeys");
            migrationBuilder.DropTable(name: "SecurityAuditLog");
        }
    }
}
