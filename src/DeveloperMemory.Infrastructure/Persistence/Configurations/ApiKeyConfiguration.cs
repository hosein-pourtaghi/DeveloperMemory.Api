using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for persistent API key storage.
/// Key hashes are indexed for fast authentication lookups.
/// </summary>
public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.KeyHash)
            .IsRequired()
            .HasMaxLength(128); // SHA-256 hex = 64 chars, salted = 128

        builder.Property(e => e.KeyPrefix)
            .IsRequired()
            .HasMaxLength(12); // "dm_" + 8 chars

        builder.Property(e => e.OwnerId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.OwnerDisplayName)
            .HasMaxLength(200);

        builder.Property(e => e.RevokedReason)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(e => e.KeyHash)
            .IsUnique(); // One hash → one key

        builder.HasIndex(e => e.OwnerId);
        builder.HasIndex(e => new { e.OwnerId, e.CreatedAt });
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => e.RevokedAt);
        builder.HasIndex(e => e.LastUsedAt);
        builder.HasIndex(e => e.ReplacedByKeyId);
    }
}
