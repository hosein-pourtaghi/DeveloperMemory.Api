using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PromptProfileVersion.
/// </summary>
public class PromptProfileVersionConfiguration : IEntityTypeConfiguration<PromptProfileVersion>
{
    public void Configure(EntityTypeBuilder<PromptProfileVersion> builder)
    {
        builder.ToTable("PromptProfileVersions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ConfigurationJson)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(200);

        builder.Property(e => e.ChangeDescription)
            .HasMaxLength(2000);

        // Indexes
        builder.HasIndex(e => new { e.PromptProfileId, e.Version })
            .IsUnique();

        builder.HasIndex(e => e.PromptProfileId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.CreatedAt);

        // Foreign key to PromptProfile
        builder.HasOne<PromptProfile>()
            .WithMany()
            .HasForeignKey(e => e.PromptProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
