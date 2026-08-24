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
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Importance = table.Column<double>(type: "double precision", precision: 3, scale: 2, nullable: false),
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
                name: "IX_MemoryEntries_State",
                table: "MemoryEntries",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_SupersededById",
                table: "MemoryEntries",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryEntries");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
