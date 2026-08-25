using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

public class PromptExperimentAssignmentConfiguration : IEntityTypeConfiguration<PromptExperimentAssignment>
{
    public void Configure(EntityTypeBuilder<PromptExperimentAssignment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AssignmentKeyHash)
            .IsRequired()
            .HasMaxLength(64); // SHA-256 hex

        // Unique constraint: same experiment + same key hash = same variant
        builder.HasIndex(e => new { e.ExperimentId, e.AssignmentKeyHash })
            .IsUnique();

        builder.HasIndex(e => e.ExperimentId);

        builder.HasOne<PromptExperiment>()
            .WithMany()
            .HasForeignKey(e => e.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PromptExperimentVariant>()
            .WithMany()
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
