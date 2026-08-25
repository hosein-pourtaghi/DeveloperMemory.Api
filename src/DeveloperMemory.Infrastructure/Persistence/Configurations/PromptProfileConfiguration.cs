using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PromptProfile.
/// </summary>
public class PromptProfileEfConfiguration : IEntityTypeConfiguration<PromptProfile>
{
    public void Configure(EntityTypeBuilder<PromptProfile> builder)
    {
        builder.ToTable("PromptProfiles");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.ConfigurationJson)
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.Name)
            .IsUnique();

        builder.HasIndex(e => e.Enabled);

        // Concurrency token
        builder.Property(e => e.Version)
            .IsConcurrencyToken();
    }
}
