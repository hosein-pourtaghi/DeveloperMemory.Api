using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;

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

        // pgvector mapping: the Vector column is a PostgreSQL `vector` type.
        // Npgsql 10 has no built-in float[]↔vector mapping, so the pgvector
        // type-mapping plugin (Pgvector.EntityFrameworkCore, enabled via
        // UseVector() in the UseNpgsql options) registers the Vector CLR type.
        // The domain model keeps float[] — a value converter bridges it to
        // Pgvector.Vector, which the plugin maps natively.
        builder.Property(e => e.Vector)
            .HasColumnType("vector")
            .HasConversion(
                v => new Vector(v),
                v => v.ToArray(),
                new VectorEntryValueComparer());

        // Indexes for common query patterns
        builder.HasIndex(e => e.MemoryId)
            .IsUnique(); // One vector per memory

        builder.HasIndex(e => new { e.Provider, e.Model });
        builder.HasIndex(e => new { e.Provider, e.Model, e.Dimensions });
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.UpdatedAt);

    }
}

/// <summary>
/// Snapshot/clone comparer for the Vector float[] property.
/// Required so EF change tracking works correctly with the value converter.
/// </summary>
public sealed class VectorEntryValueComparer
    : ValueComparer<float[]>
{
    public VectorEntryValueComparer()
        : base(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (acc, f) => acc ^ f.GetHashCode()),
            v => v.ToArray())
    {
    }
}
