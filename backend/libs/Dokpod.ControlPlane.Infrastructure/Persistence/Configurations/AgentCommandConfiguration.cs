using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dokpod.ControlPlane.Infrastructure.Persistence.Configurations;

public sealed class AgentCommandConfiguration : IEntityTypeConfiguration<AgentCommandEntity>
{
    public void Configure(EntityTypeBuilder<AgentCommandEntity> entity)
    {
        entity.ToTable("agent_commands");
        entity.HasKey(command => new { command.EnvironmentId, command.CommandId, command.CreatedAtUtc });
        entity.Property(command => command.EnvironmentId).HasColumnName("environment_id");
        entity.Property(command => command.CommandId).HasColumnName("command_id");
        entity.Property(command => command.Kind).HasColumnName("kind").HasConversion<short>();
        entity.Property(command => command.ContainerId).HasColumnName("container_id").HasMaxLength(128);
        entity.Property(command => command.ExpectedContainerRevision).HasColumnName("expected_container_revision").HasMaxLength(255);
        entity.Property(command => command.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
        entity.Property(command => command.DeadlineUtc).HasColumnName("deadline_utc").IsRequired();
        entity.Property(command => command.FencingToken).HasColumnName("fencing_token").IsRequired();
        entity.Property(command => command.LastDispatchFencingToken).HasColumnName("last_dispatch_fencing_token");
        entity.Property(command => command.State).HasColumnName("state").HasConversion<short>();
        entity.Property(command => command.FailureCode).HasColumnName("failure_code").HasMaxLength(128);
        entity.Property(command => command.ObservedContainerRevision).HasColumnName("observed_container_revision").HasMaxLength(255);
        entity.Property(command => command.CompletedAtUtc).HasColumnName("completed_at_utc");
        entity.Property(command => command.ResolvedImageDigest).HasColumnName("resolved_image_digest").HasMaxLength(160);
        entity.Property(command => command.ImageSizeBytes).HasColumnName("image_size_bytes");
        entity.Property(command => command.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        entity.Property(command => command.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        entity.HasOne<EnvironmentRegistrationEntity>()
            .WithMany()
            .HasForeignKey(command => command.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(command => new { command.EnvironmentId, command.State, command.CreatedAtUtc });
        entity.HasIndex(command => new { command.State, command.DeadlineUtc });
    }
}
public sealed class AgentCommandParametersConfiguration : IEntityTypeConfiguration<AgentCommandParametersEntity>
{
    public void Configure(EntityTypeBuilder<AgentCommandParametersEntity> entity)
    {
        entity.ToTable("agent_command_parameters");
        entity.HasKey(parameters => new { parameters.EnvironmentId, parameters.CommandId, parameters.CreatedAtUtc });
        entity.Property(parameters => parameters.EnvironmentId).HasColumnName("environment_id");
        entity.Property(parameters => parameters.CommandId).HasColumnName("command_id");
        entity.Property(parameters => parameters.CreatedAtUtc).HasColumnName("created_at_utc");
        entity.Property(parameters => parameters.Kind).HasColumnName("kind").HasMaxLength(32);
        entity.Property(parameters => parameters.Parameters).HasColumnName("parameters").HasColumnType("jsonb");
    }
}

public sealed class InventoryImageProjectionConfiguration : IEntityTypeConfiguration<InventoryImageProjectionEntity>
{
    public void Configure(EntityTypeBuilder<InventoryImageProjectionEntity> entity)
    {
        entity.ToTable("inventory_image_projections");
        entity.HasKey(projection => projection.EnvironmentId);
        entity.Property(projection => projection.EnvironmentId).HasColumnName("environment_id");
        entity.Property(projection => projection.Revision).HasColumnName("revision");
        entity.Property(projection => projection.ObservedAtUtc).HasColumnName("observed_at_utc");
    }
}

public sealed class InventoryImageConfiguration : IEntityTypeConfiguration<InventoryImageEntity>
{
    public void Configure(EntityTypeBuilder<InventoryImageEntity> entity)
    {
        entity.ToTable("inventory_images");
        entity.HasKey(image => new { image.EnvironmentId, image.ImageId });
        entity.Property(image => image.EnvironmentId).HasColumnName("environment_id");
        entity.Property(image => image.ImageId).HasColumnName("image_id").HasMaxLength(160);
        entity.Property(image => image.RepoTags).HasColumnName("repo_tags");
        entity.Property(image => image.RepoDigests).HasColumnName("repo_digests");
        entity.Property(image => image.SizeBytes).HasColumnName("size_bytes");
        entity.Property(image => image.CreatedAt).HasColumnName("created_at");
        entity.Property(image => image.Architecture).HasColumnName("architecture").HasMaxLength(32);
        entity.Property(image => image.Os).HasColumnName("os").HasMaxLength(32);
        entity.Property(image => image.Truncated).HasColumnName("truncated");
        entity.Property(image => image.ObservedAt).HasColumnName("observed_at");
        entity.Property(image => image.Revision).HasColumnName("revision");
    }
}
