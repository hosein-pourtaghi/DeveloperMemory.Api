using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PromptAuditEvent.
/// </summary>
public class PromptAuditEventConfiguration : IEntityTypeConfiguration<PromptAuditEvent>
{
    public void Configure(EntityTypeBuilder<PromptAuditEvent> builder)
    {
        builder.ToTable("PromptAuditEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.EventType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Details)
            .HasMaxLength(4000);

        builder.Property(e => e.UserId)
            .HasMaxLength(200);

        // Indexes
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.ProcessingRecordId);
    }
}
