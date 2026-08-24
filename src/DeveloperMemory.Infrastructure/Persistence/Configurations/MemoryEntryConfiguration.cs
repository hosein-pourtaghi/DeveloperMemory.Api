using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeveloperMemory.Infrastructure.Persistence.Configurations;

public class MemoryEntryConfiguration : IEntityTypeConfiguration<MemoryEntry>
{
    public void Configure(EntityTypeBuilder<MemoryEntry> builder)
    {
        builder.ToTable("MemoryEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.Scope)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.State)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Classification)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Source)
            .HasMaxLength(500);

        builder.Property(e => e.Importance)
            .HasPrecision(3, 2);

        builder.HasIndex(e => e.Scope);
        builder.HasIndex(e => e.State);
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.Classification);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => new { e.Scope, e.ProjectId, e.State });

        builder.HasOne(e => e.Project)
            .WithMany(p => p.Memories)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SupersededBy)
            .WithMany()
            .HasForeignKey(e => e.SupersededById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
