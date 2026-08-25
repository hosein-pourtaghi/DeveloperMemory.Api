using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

public class PromptExperimentVariantConfiguration : IEntityTypeConfiguration<PromptExperimentVariant>
{
    public void Configure(EntityTypeBuilder<PromptExperimentVariant> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.OptimizationMode)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.ExperimentId, e.Enabled });
        builder.HasIndex(e => e.ExperimentId);

        builder.HasOne<PromptExperiment>()
            .WithMany()
            .HasForeignKey(e => e.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
