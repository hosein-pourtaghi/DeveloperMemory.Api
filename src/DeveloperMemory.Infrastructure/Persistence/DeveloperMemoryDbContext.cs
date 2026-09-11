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
    public DbSet<DiagnosticLogEntry> DiagnosticLogs => Set<DiagnosticLogEntry>();

    public DeveloperMemoryDbContext(DbContextOptions<DeveloperMemoryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // pgvector support is PostgreSQL-specific and is applied only on the
        // Npgsql provider. The EF InMemory provider (used by tests) cannot map
        // the Pgvector.Vector provider type produced by the VectorEntry value
        // converter, so the model is shaped per provider.
        if (Database.IsNpgsql())
        {
            // pgvector extension is required for the VectorEntries.Vector column.
            // Declaring it here makes the requirement explicit in the EF model and
            // emitted migrations (CREATE EXTENSION IF NOT EXISTS vector).
            modelBuilder.HasPostgresExtension("vector");
        }

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeveloperMemoryDbContext).Assembly);

        if (!Database.IsNpgsql())
        {
            // VectorEntryConfiguration maps Vector as a pgvector column via a
            // float[] → Pgvector.Vector value converter. Other providers (the
            // InMemory provider) have no mapping for that provider type and would
            // fail model validation, so the property is excluded from their model.
            // Vector data always flows through IVectorStore implementations
            // (InMemoryVectorStore in tests), never through provider-generic LINQ,
            // so excluding the column from non-PostgreSQL models has no functional
            // impact. Design-time and migrations are Npgsql-only, so snapshots and
            // emitted migrations are unaffected.
            modelBuilder.Entity<VectorEntry>().Ignore(e => e.Vector);
        }
    }
}
