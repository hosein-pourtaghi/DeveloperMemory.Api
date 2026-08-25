using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

public class PromptExperimentResultConfiguration : IEntityTypeConfiguration<PromptExperimentResult>
{
    public void Configure(EntityTypeBuilder<PromptExperimentResult> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.ExperimentId, e.VariantId });
        builder.HasIndex(e => e.CreatedAt);
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
