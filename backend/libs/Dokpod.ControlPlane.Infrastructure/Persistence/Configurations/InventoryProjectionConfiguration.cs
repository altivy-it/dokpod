using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dokpod.ControlPlane.Infrastructure.Persistence.Configurations;

public sealed class InventoryProjectionConfiguration : IEntityTypeConfiguration<InventoryProjectionEntity>
{
    public void Configure(EntityTypeBuilder<InventoryProjectionEntity> builder)
    {
        builder.ToTable("inventory_projections");
        builder.HasKey(projection => projection.EnvironmentId);
        builder.Property(projection => projection.EnvironmentId).HasColumnName("environment_id");
        builder.Property(projection => projection.Revision).HasColumnName("revision").IsRequired();
        builder.Property(projection => projection.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.HasMany(projection => projection.Containers)
            .WithOne(container => container.Projection)
            .HasForeignKey(container => container.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InventoryContainerConfiguration : IEntityTypeConfiguration<InventoryContainerEntity>
{
    public void Configure(EntityTypeBuilder<InventoryContainerEntity> builder)
    {
        builder.ToTable("inventory_containers");
        builder.HasKey(container => new { container.EnvironmentId, container.ContainerId });
        builder.Property(container => container.EnvironmentId).HasColumnName("environment_id");
        builder.Property(container => container.ContainerId).HasColumnName("container_id").HasMaxLength(128);
        builder.Property(container => container.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(container => container.ImageReference).HasColumnName("image_reference").HasMaxLength(512);
        builder.Property(container => container.State).HasColumnName("state").HasMaxLength(32);
        builder.Property(container => container.Revision).HasColumnName("revision").HasMaxLength(255);
        builder.Property(container => container.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.Property(container => container.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(container => container.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(container => container.Health).HasColumnName("health").HasMaxLength(16);
        builder.Property(container => container.RestartCount).HasColumnName("restart_count");
        builder.Property(container => container.ExitCode).HasColumnName("exit_code");
        builder.Property(container => container.OomKilled).HasColumnName("oom_killed");
        builder.Property(container => container.Ports).HasColumnName("ports").HasColumnType("jsonb");
        builder.Property(container => container.ImageId).HasColumnName("image_id").HasMaxLength(160);
        builder.Property(container => container.DisplayStatus).HasColumnName("display_status").HasMaxLength(16).IsRequired();
    }
}