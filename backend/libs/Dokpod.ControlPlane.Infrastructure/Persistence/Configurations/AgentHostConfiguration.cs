using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dokpod.ControlPlane.Infrastructure.Persistence.Configurations;

public sealed class AgentDescriptorConfiguration : IEntityTypeConfiguration<AgentDescriptorEntity>
{
    public void Configure(EntityTypeBuilder<AgentDescriptorEntity> builder)
    {
        builder.ToTable("agent_descriptors");
        builder.HasKey(item => item.EnvironmentId);
        builder.Property(item => item.EnvironmentId).HasColumnName("environment_id");
        builder.Property(item => item.EngineKind).HasColumnName("engine_kind").HasMaxLength(16);
        builder.Property(item => item.EngineVersion).HasColumnName("engine_version").HasMaxLength(64);
        builder.Property(item => item.EngineApiVersion).HasColumnName("engine_api_version").HasMaxLength(32);
        builder.Property(item => item.EngineRootless).HasColumnName("engine_rootless");
        builder.Property(item => item.OperatingSystem).HasColumnName("operating_system").HasMaxLength(16);
        builder.Property(item => item.OperatingSystemVersion).HasColumnName("operating_system_version").HasMaxLength(128);
        builder.Property(item => item.Architecture).HasColumnName("architecture").HasMaxLength(16);
        builder.Property(item => item.AgentVersion).HasColumnName("agent_version").HasMaxLength(64);
        builder.Property(item => item.Capabilities).HasColumnName("capabilities").HasMaxLength(1024);
        builder.Property(item => item.HostName).HasColumnName("host_name").HasMaxLength(255);
        builder.Property(item => item.CpuCount).HasColumnName("cpu_count");
        builder.Property(item => item.MemoryBytes).HasColumnName("memory_bytes");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}

public sealed class AgentConnectionConfiguration : IEntityTypeConfiguration<AgentConnectionEntity>
{
    public void Configure(EntityTypeBuilder<AgentConnectionEntity> builder)
    {
        builder.ToTable("agent_connections");
        builder.HasKey(item => item.EnvironmentId);
        builder.Property(item => item.EnvironmentId).HasColumnName("environment_id");
        builder.Property(item => item.Connected).HasColumnName("connected");
        builder.Property(item => item.SessionStartedAtUtc).HasColumnName("session_started_at_utc");
        builder.Property(item => item.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(item => item.DisconnectedAtUtc).HasColumnName("disconnected_at_utc");
    }
}

public sealed class EnvironmentEventConfiguration : IEntityTypeConfiguration<EnvironmentEventEntity>
{
    public void Configure(EntityTypeBuilder<EnvironmentEventEntity> builder)
    {
        builder.ToTable("environment_events");
        builder.HasKey(item => new { item.EventId, item.OccurredAt });
        builder.Property(item => item.EventId).HasColumnName("event_id");
        builder.Property(item => item.EnvironmentId).HasColumnName("environment_id");
        builder.Property(item => item.Type).HasColumnName("type").HasMaxLength(64);
        builder.Property(item => item.Severity).HasColumnName("severity").HasMaxLength(16);
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at");
        builder.Property(item => item.ReceivedAt).HasColumnName("received_at");
        builder.Property(item => item.SubjectKind).HasColumnName("subject_kind").HasMaxLength(16);
        builder.Property(item => item.SubjectId).HasColumnName("subject_id").HasMaxLength(255);
        builder.Property(item => item.SubjectName).HasColumnName("subject_name").HasMaxLength(255);
        builder.Property(item => item.Parameters).HasColumnName("parameters").HasColumnType("jsonb");
        builder.Property(item => item.AggregatedCount).HasColumnName("aggregated_count");
        builder.Property(item => item.SourceKey).HasColumnName("source_key").HasMaxLength(255);
    }
}
