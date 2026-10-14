using System.Text;
using Dokpod.ControlPlane.Api;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 013 T007/T018 em PostgreSQL real: valores cifrados, reentrega, purga após terminal e canários.</summary>
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class ContainerCreationIntegrationTests
{
    private const string Canary = "canario-013-valor-secreto";

    private static AgentCommand Create(Guid env, string name, string value = Canary)
    {
        Assert.True(ImageReference.TryParse("nginx:1.27", out var image));
        var spec = new ContainerCreationSpec(image, name, [new PortSpec(null, 8081, 80, "tcp")],
            [new EnvironmentSpec("DATABASE_URL", value, null, true), new EnvironmentSpec("JWT", null, new HostSecretReference(HostSecretKind.PodmanSecret, "jwt"), true)],
            [], null, 2, 512L * 1024 * 1024, new RestartSpec(RestartPolicyKind.UnlessStopped), null, false);
        var parameters = new CreateContainerParameters(spec);
        return new AgentCommand(env, Guid.NewGuid(), AgentCommandKind.CreateContainer, "", "",
            AgentCommandKinds.PayloadHash(AgentCommandKind.CreateContainer, "", "", parameters),
            DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds()), 1)
        {
            Parameters = parameters,
        };
    }

    [Fact]
    public async Task ProtectedValues_AreEncryptedRedeliveredAndPurgedAfterTerminal()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        var protector = new DataProtectionCommandParameterProtector(new EphemeralDataProtectionProvider());
        var store = new PostgresAgentCommandStore(context, new PostgresAuditEventWriter(context), protector);
        var now = DateTimeOffset.UtcNow;
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var command = Create(env, "api-pedidos");
            Assert.Equal(AgentCommandEnqueueResult.Created, await store.EnqueueAsync(new PersistedAgentCommand(command, ControlPlaneCommandState.Pending, now, now), ct));

            var row = await context.AgentCommandParameters.AsNoTracking().SingleAsync(p => p.EnvironmentId == env, ct);
            Assert.DoesNotContain(Canary, row.Parameters);
            Assert.Contains("DATABASE_URL", row.Parameters);
            Assert.NotNull(row.ProtectedValues);
            Assert.DoesNotContain(Canary, Encoding.UTF8.GetString(row.ProtectedValues!));

            var claimed = Assert.Single(await store.ClaimDispatchableAsync(env, 3, now.AddSeconds(1), ct));
            var spec = Assert.IsType<CreateContainerParameters>(claimed.Command.Parameters).Spec;
            Assert.Equal(Canary, spec.Environment[0].Value);
            Assert.True(spec.ValuesAvailable);
            Assert.Equal(command.PayloadHash, AgentCommandKinds.PayloadHash(AgentCommandKind.CreateContainer, "", "", new CreateContainerParameters(spec)));

            var status = await store.GetAsync(env, command.CommandId, ct);
            Assert.Equal("api-pedidos", status!.ContainerName);

            var done = new AgentCommandStatusUpdate(env, command.CommandId, ControlPlaneCommandState.Succeeded, null, null, now.AddSeconds(5))
            {
                Output = new AgentCommandOutput(CreatedContainerId: new string('c', 64)),
            };
            Assert.Equal(AgentCommandStatusUpdateResult.Applied, await store.ApplyStatusAsync(done, ct));
            row = await context.AgentCommandParameters.AsNoTracking().SingleAsync(p => p.EnvironmentId == env, ct);
            Assert.Null(row.ProtectedValues);
            Assert.NotNull(row.PurgedAt);
            Assert.Equal(new string('c', 64), (await store.GetAsync(env, command.CommandId, ct))!.Output!.CreatedContainerId);

            // Canário ausente de todo o banco do ambiente após o estado terminal (SC-003).
            var leaks = await context.Database.SqlQuery<int>($"""
                SELECT (SELECT count(*)::int FROM dokpod.agent_command_parameters WHERE environment_id = {env} AND (parameters::text LIKE {'%' + Canary + '%'} OR protected_values IS NOT NULL))
                     + (SELECT count(*)::int FROM dokpod.agent_commands WHERE environment_id = {env} AND row_to_json(agent_commands)::text LIKE {'%' + Canary + '%'})
                     + (SELECT count(*)::int FROM dokpod.audit_events WHERE environment_id = {env} AND row_to_json(audit_events)::text LIKE {'%' + Canary + '%'}) AS "Value"
                """).SingleAsync(ct);
            Assert.Equal(0, leaks);
        }
        finally
        {
            await CleanupAsync(context, env, ct);
        }
    }

    [Fact]
    public async Task LostKeyRingOrPurgedValues_DispatchWithoutValues()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var writer = new PostgresAgentCommandStore(context, new PostgresAuditEventWriter(context), new DataProtectionCommandParameterProtector(new EphemeralDataProtectionProvider()));
            await writer.EnqueueAsync(new PersistedAgentCommand(Create(env, "perdido"), ControlPlaneCommandState.Pending, now, now), ct);

            // Outro key ring (perda das chaves): os valores não são recuperáveis.
            var reader = new PostgresAgentCommandStore(context, new PostgresAuditEventWriter(context), new DataProtectionCommandParameterProtector(new EphemeralDataProtectionProvider()));
            var claimed = Assert.Single(await reader.ClaimDispatchableAsync(env, 4, now.AddSeconds(1), ct));
            var spec = Assert.IsType<CreateContainerParameters>(claimed.Command.Parameters).Spec;
            Assert.False(spec.ValuesAvailable);
            Assert.Null(spec.Environment[0].Value);
        }
        finally
        {
            await CleanupAsync(context, env, ct);
        }
    }

    [Fact]
    public async Task Schema_AcceptsKind8OnlyAsHostTarget()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var invalid = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO dokpod.agent_commands (environment_id, command_id, kind, container_id, expected_container_revision,
                    payload_hash, deadline_utc, fencing_token, state, created_at_utc, updated_at_utc)
                VALUES ({env}, {Guid.NewGuid()}, 8, {new string('a', 64)}, 'r', {new string('A', 64)}, {now.AddMinutes(1)}, 1, 0, {now}, {now})
                """, ct));
            Assert.Equal("23514", invalid.SqlState);
        }
        finally
        {
            await CleanupAsync(context, env, ct);
        }
    }

    private static Task CreateEnvironmentAsync(ControlPlaneDbContext context, Guid id, CancellationToken ct) =>
        new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(id, $"cc-{id:N}"[..20], "srv-cc", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.CreateContainer]), ct);

    private static async Task CleanupAsync(ControlPlaneDbContext context, Guid env, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.agent_commands WHERE environment_id = {env}", ct);
        await context.EnvironmentRegistrations.Where(item => item.EnvironmentId == env).ExecuteDeleteAsync(ct);
    }

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}
