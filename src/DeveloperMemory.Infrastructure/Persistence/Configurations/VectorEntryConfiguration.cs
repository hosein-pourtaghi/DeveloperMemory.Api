using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for VectorEntry.
/// Maps to PostgreSQL table with pgvector support.
/// </summary>
public class VectorEntryConfiguration : IEntityTypeConfiguration<VectorEntry>
{
    public void Configure(EntityTypeBuilder<VectorEntry> builder)
    {
        builder.ToTable("VectorEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Model)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Version)
            .HasMaxLength(100);

        builder.Property(e => e.ContentHash)
            .IsRequired()
            .HasMaxLength(50);

        // Indexes for common query patterns
        builder.HasIndex(e => e.MemoryId)
            .IsUnique(); // One vector per memory

        builder.HasIndex(e => new { e.Provider, e.Model });
        builder.HasIndex(e => new { e.Provider, e.Model, e.Dimensions });
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.UpdatedAt);

        // Note: pgvector extension must be enabled in the database
        // CREATE EXTENSION IF NOT EXISTS vector;
        //
        // The Vector column uses Npgsql's Vector type mapping.
        // Configure in OnModelCreating or via Npgsql configuration.
        //
        // For pgvector, the column type should be:
        // ALTER TABLE "VectorEntries" ALTER COLUMN "Vector" TYPE vector(dimensions);
    }
}
