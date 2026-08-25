using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PromptProcessingRecord.
/// </summary>
public class PromptProcessingRecordConfiguration : IEntityTypeConfiguration<PromptProcessingRecord>
{
    public void Configure(EntityTypeBuilder<PromptProcessingRecord> builder)
    {
        builder.ToTable("PromptProcessingRecords");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Intent)
            .HasMaxLength(50);

        builder.Property(e => e.TaskType)
            .HasMaxLength(50);

        builder.Property(e => e.OptimizationMode)
            .HasMaxLength(50);

        builder.Property(e => e.Optimizer)
            .HasMaxLength(100);

        builder.Property(e => e.OptimizerVersion)
            .HasMaxLength(50);

        builder.Property(e => e.Model)
            .HasMaxLength(200);

        builder.Property(e => e.ValidationStatus)
            .HasMaxLength(50);

        builder.Property(e => e.ExperimentId)
            .HasMaxLength(100);

        builder.Property(e => e.VariantId)
            .HasMaxLength(100);

        builder.Property(e => e.UserId)
            .HasMaxLength(200);

        builder.Property(e => e.WorkspaceId)
            .HasMaxLength(200);

        builder.Property(e => e.MemoryIdsUsed)
            .HasMaxLength(4000);

        builder.Property(e => e.QualityGateFailureReason)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ProfileId);
        builder.HasIndex(e => e.ValidationStatus);
        builder.HasIndex(e => e.ExperimentId);
        builder.HasIndex(e => e.UserId);
    }
}
