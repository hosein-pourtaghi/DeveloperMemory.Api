using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeveloperMemory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnablePgvectorSemanticRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Register the pgvector extension in the EF model/database annotation.
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            // Guarantee the extension exists before converting the column
            // (databases migrated before this change never created it).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            // real[] → vector: pgvector's text form is "[1,2,3]" while PostgreSQL
            // array text is "{1,2,3}", so a plain cast fails. Convert explicitly
            // through the bracketed text form. All existing vector data is
            // preserved (row values are reinterpreted, never dropped).
            migrationBuilder.Sql(
                "ALTER TABLE \"VectorEntries\" ALTER COLUMN \"Vector\" SET DATA TYPE vector " +
                "USING ('[' || array_to_string(\"Vector\", ',') || ']')::vector;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // vector → real[]: reverse conversion through the brace form.
            // Also data-preserving.
            migrationBuilder.Sql(
                "ALTER TABLE \"VectorEntries\" ALTER COLUMN \"Vector\" SET DATA TYPE real[] " +
                "USING ('{' || btrim(\"Vector\"::text, '[]') || '}')::real[];");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
