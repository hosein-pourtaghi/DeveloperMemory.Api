using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for persistent security audit log.
/// Append-only table with indexes for timestamp, event type, and owner queries.
/// </summary>
public class SecurityAuditLogEntryConfiguration : IEntityTypeConfiguration<SecurityAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLogEntry> builder)
    {
        builder.ToTable("SecurityAuditLog");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Outcome)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.OwnerId)
            .HasMaxLength(200);

        builder.Property(e => e.KeyId)
            .HasMaxLength(100);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(100);

        builder.Property(e => e.SourceIp)
            .HasMaxLength(50);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(500);

        builder.Property(e => e.MetadataJson)
            .HasMaxLength(4000);

        // Indexes for common query patterns
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.OwnerId);
        builder.HasIndex(e => new { e.EventType, e.OccurredAt });
        builder.HasIndex(e => new { e.OwnerId, e.OccurredAt });
    }
}
