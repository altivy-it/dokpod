using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
    : DbContext(options)
{
    public DbSet<AgentIdentityEntity> AgentIdentities => Set<AgentIdentityEntity>();
    public DbSet<AuditEventKeyEntity> AuditEventKeys => Set<AuditEventKeyEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<AgentCommandEntity> AgentCommands => Set<AgentCommandEntity>();
    public DbSet<AgentCommandParametersEntity> AgentCommandParameters => Set<AgentCommandParametersEntity>();
    public DbSet<EnvironmentRegistrationEntity> EnvironmentRegistrations => Set<EnvironmentRegistrationEntity>();
    public DbSet<InventoryProjectionEntity> InventoryProjections => Set<InventoryProjectionEntity>();
    public DbSet<InventoryContainerEntity> InventoryContainers => Set<InventoryContainerEntity>();
    public DbSet<AgentDescriptorEntity> AgentDescriptors => Set<AgentDescriptorEntity>();
    public DbSet<AgentConnectionEntity> AgentConnections => Set<AgentConnectionEntity>();
    public DbSet<EnvironmentEventEntity> EnvironmentEvents => Set<EnvironmentEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ControlPlaneSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ControlPlaneDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
