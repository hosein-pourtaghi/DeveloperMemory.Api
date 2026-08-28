using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

public class DeveloperMemoryDbContext : DbContext
{
    public DbSet<MemoryEntry> MemoryEntries => Set<MemoryEntry>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<VectorEntry> VectorEntries => Set<VectorEntry>();
    public DbSet<PromptProfile> PromptProfiles => Set<PromptProfile>();
    public DbSet<PromptProfileVersion> PromptProfileVersions => Set<PromptProfileVersion>();
    public DbSet<PromptProcessingRecord> PromptProcessingRecords => Set<PromptProcessingRecord>();
    public DbSet<PromptAuditEvent> PromptAuditEvents => Set<PromptAuditEvent>();
    public DbSet<PromptExperiment> PromptExperiments => Set<PromptExperiment>();
    public DbSet<PromptExperimentVariant> PromptExperimentVariants => Set<PromptExperimentVariant>();
    public DbSet<PromptExperimentAssignment> PromptExperimentAssignments => Set<PromptExperimentAssignment>();
    public DbSet<PromptExperimentResult> PromptExperimentResults => Set<PromptExperimentResult>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SecurityAuditLogEntry> SecurityAuditLog => Set<SecurityAuditLogEntry>();

    public DeveloperMemoryDbContext(DbContextOptions<DeveloperMemoryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeveloperMemoryDbContext).Assembly);
    }
}
