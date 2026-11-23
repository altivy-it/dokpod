using Dokpod.ControlPlane.Api.Agents;
using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ChangeWindows;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T030: a exceção de emergência sobre PostgreSQL real. O evento da ação e o <c>change_window.override</c>
/// entram na mesma transação do comando (ou nenhum), a repetição não duplica a exceção, a exceção vale para uma ação só
/// e o mesmo texto atende à política de justificativa da 016.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class ChangeWindowOverrideTests
{
    // Terça 24/11/2026, 15:00 em São Paulo: fora da janela seg-sex 22:00-06:00.
    private static readonly DateTimeOffset Outside = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly string Container = new('f', 64);
    private const string Justification = "Incidente INC-123 " + "tok" + "en=abc123segredo";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AcceptedOverride_RecordsTheActionAndTheOverrideTogether_WithTheMaskedJustification()
    {
        await using var fixture = await Fixture.CreateAsync();
        var commandId = Guid.NewGuid();

        var result = await fixture.SubmitAsync(commandId, AgentCommandKind.RestartContainer, new ChangeWindowOverrideRequest(Justification), Justification);

        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, result.Outcome);
        var events = await fixture.AuditAsync(commandId);
        Assert.Equal(["change_window.override", "container.restart"], events.Select(item => item.Action).Order(StringComparer.Ordinal));
        var overrideEvent = events.Single(item => item.Action == "change_window.override");
        Assert.Equal("user-026", overrideEvent.Actor);
        Assert.Contains("token=***", overrideEvent.Details);
        Assert.DoesNotContain("abc123segredo", overrideEvent.Details);
        Assert.Equal(1L, await fixture.CommandCountAsync());
    }

    [Fact]
    public async Task FailedCommandInsert_LeavesNeitherTheActionNorTheOverride()
    {
        await using var fixture = await Fixture.CreateAsync();
        var commandId = Guid.NewGuid();
        // O cadastro em memória conhece o host, mas ele não existe no banco: o INSERT do comando falha pela chave estrangeira.
        fixture.Registrations.Add(EnvironmentRegistration.Create(fixture.Ghost, "cw-ghost", "srv-ghost", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production));
        await fixture.Sessions.ActivateAsync(fixture.Ghost, Ct);

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Commands.SubmitAsync(
            fixture.Ghost, commandId, AgentCommandKind.RestartContainer, Container, "rev-1", Outside.AddMinutes(10), Actor, "token", Guid.NewGuid(), Ct,
            Justification, new ChangeWindowOverrideRequest(Justification)));

        Assert.Empty(await fixture.AuditAsync(commandId));
    }

    [Fact]
    public async Task RepeatedOverride_IsADuplicate_WithoutASecondOverrideEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var commandId = Guid.NewGuid();
        var request = new ChangeWindowOverrideRequest(Justification);

        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, (await fixture.SubmitAsync(commandId, AgentCommandKind.RestartContainer, request, Justification)).Outcome);
        Assert.Equal(ContainerCommandSubmissionOutcome.Duplicate, (await fixture.SubmitAsync(commandId, AgentCommandKind.RestartContainer, request, Justification)).Outcome);

        Assert.Single(await fixture.AuditAsync(commandId), item => item.Action == "change_window.override");
        Assert.Equal(1L, await fixture.CommandCountAsync());
    }

    [Fact]
    public async Task TheOverrideCoversOneActionOnly_TheNextOneIsRefused()
    {
        await using var fixture = await Fixture.CreateAsync();

        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, (await fixture.SubmitAsync(Guid.NewGuid(), AgentCommandKind.RestartContainer, new ChangeWindowOverrideRequest(Justification), Justification)).Outcome);
        var next = await fixture.SubmitAsync(Guid.NewGuid(), AgentCommandKind.RestartContainer, null, null);

        Assert.Equal(ContainerCommandSubmissionOutcome.ChangeWindowDenied, next.Outcome);
        Assert.Equal(ChangeWindowGuard.WindowClosed, next.FailureCode);
    }

    [Fact]
    public async Task OverrideWithoutTheScope_IsRefusedAndAudited_WithoutCommand()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Decider.DenyOverride = true;

        var denied = await fixture.SubmitAsync(Guid.NewGuid(), AgentCommandKind.RestartContainer, new ChangeWindowOverrideRequest(Justification), Justification);

        Assert.Equal((ContainerCommandSubmissionOutcome.ChangeWindowDenied, ChangeWindowGuard.OverrideDenied), (denied.Outcome, denied.FailureCode));
        Assert.Equal(0L, await fixture.CommandCountAsync());
        Assert.Equal(1L, await fixture.ActionCountAsync("change_window.denied"));
    }

    [Fact]
    public async Task WithTheJustificationPolicyOfThe016_TheSameTextServesBothRules()
    {
        await using var fixture = await Fixture.CreateAsync();
        var justifications = new PostgresActionJustificationStore(fixture.Context);
        var previous = (await justifications.GetPoliciesAsync(Ct)).GetValueOrDefault(EnvironmentStage.Production);
        await justifications.SetPolicyAsync(EnvironmentStage.Production, true, "teste-026", DateTimeOffset.UtcNow, Ct);
        try
        {
            var commandId = Guid.NewGuid();
            var stopped = await fixture.SubmitAsync(commandId, AgentCommandKind.StopContainer, new ChangeWindowOverrideRequest(Justification), Justification);

            Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, stopped.Outcome);
            await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
            Assert.Equal(1L, await ScalarAsync<long>(
                connection, null, "SELECT count(*) FROM dokpod.container_action_justifications WHERE command_id = @id", Ct, new NpgsqlParameter("id", commandId)));
            Assert.Contains(await fixture.AuditAsync(commandId), item => item.Action == "change_window.override");
        }
        finally
        {
            await justifications.SetPolicyAsync(EnvironmentStage.Production, previous, "teste-026", DateTimeOffset.UtcNow, Ct);
        }
    }

    private static AuthenticatedActor Actor => AuthenticatedActor.FromSubject("user-026") with { DisplayName = "Maria" };

    private sealed record AuditRow(string Action, string Actor, string Details);

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(string connectionString)
        {
            ConnectionString = connectionString;
            Context = CreateContext(connectionString);
            var clock = new FixedTime(Outside);
            var commandStore = new PostgresAgentCommandStore(Context);
            var audit = new PostgresAuditEventWriter(Context);
            var queue = new AgentCommandQueueService(commandStore, new InMemoryAgentCommandDeliveryQueue(), clock);
            var guard = new ChangeWindowGuard(new ChangeWindowStore(Context), commandStore, Decider, audit, clock);
            Commands = new ContainerCommandService(Decider, Registrations, Sessions, queue, guard, new PostgresActionJustificationStore(Context), new PostgresContainerSearchStore(Context), clock);
        }

        public string ConnectionString { get; }
        public ControlPlaneDbContext Context { get; }
        public OverrideDecider Decider { get; } = new();
        public InMemoryRegistrations Registrations { get; } = new();
        public InMemoryAgentSessionStore Sessions { get; } = new();
        public ContainerCommandService Commands { get; }
        public Guid Environment { get; } = Guid.NewGuid();
        public Guid Ghost { get; } = Guid.NewGuid();

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture(ConnectionStringOrSkip());
            await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
            await ExecuteAsync(connection, null, "DELETE FROM dokpod.change_windows", Ct);
            var registration = EnvironmentRegistration.Create(fixture.Environment, $"cw-{fixture.Environment:N}"[..20], "srv-cw", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production);
            await new PostgresEnvironmentRegistrationStore(fixture.Context).CreateAsync(registration, Ct);
            fixture.Registrations.Add(registration);
            Assert.Equal(ChangeWindowWriteOutcome.Saved, (await new ChangeWindowStore(fixture.Context).SaveAsync(
                new ChangeWindow(
                    EnvironmentStage.Production,
                    "America/Sao_Paulo",
                    [new ChangeWindowRange(new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }, new TimeOnly(22, 0), new TimeOnly(6, 0))],
                    0,
                    "Ana",
                    DateTimeOffset.UtcNow),
                null,
                Ct)).Outcome);
            await fixture.Sessions.ActivateAsync(fixture.Environment, Ct);
            return fixture;
        }

        public Task<ContainerCommandSubmissionResult> SubmitAsync(Guid commandId, AgentCommandKind kind, ChangeWindowOverrideRequest? overrideRequest, string? justification) =>
            Commands.SubmitAsync(Environment, commandId, kind, Container, "rev-1", Outside.AddMinutes(10), Actor, "token", Guid.NewGuid(), Ct, justification, overrideRequest);

        public async Task<IReadOnlyList<AuditRow>> AuditAsync(Guid commandId)
        {
            await using var connection = await OpenAsync(ConnectionString, Ct);
            await using var command = new NpgsqlCommand("SELECT action, actor_id, coalesce(details::text, '') FROM dokpod.audit_events WHERE command_id = @id", connection);
            command.Parameters.AddWithValue("id", commandId);
            var rows = new List<AuditRow>();
            await using var reader = await command.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct)) rows.Add(new AuditRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            return rows;
        }

        public async Task<long> CommandCountAsync()
        {
            await using var connection = await OpenAsync(ConnectionString, Ct);
            return await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.agent_commands WHERE environment_id = @id", Ct, new NpgsqlParameter("id", Environment));
        }

        public async Task<long> ActionCountAsync(string action)
        {
            await using var connection = await OpenAsync(ConnectionString, Ct);
            return await ScalarAsync<long>(
                connection, null, "SELECT count(*) FROM dokpod.audit_events WHERE environment_id = @id AND action = @action", Ct,
                new NpgsqlParameter("id", Environment), new NpgsqlParameter("action", action));
        }

        public async ValueTask DisposeAsync()
        {
            await using (var connection = await OpenAsync(ConnectionString, Ct))
            {
                await ExecuteAsync(connection, null, "DELETE FROM dokpod.change_windows", Ct);
                await ExecuteAsync(connection, null, "DELETE FROM dokpod.container_action_justifications WHERE environment_id = @id", Ct, new NpgsqlParameter("id", Environment));
                await ExecuteAsync(connection, null, "DELETE FROM dokpod.agent_commands WHERE environment_id = @id", Ct, new NpgsqlParameter("id", Environment));
            }

            await Context.EnvironmentRegistrations.Where(item => item.EnvironmentId == Environment).ExecuteDeleteAsync(Ct);
            await Context.DisposeAsync();
        }
    }

    /// <summary>Keycloak de teste: permite a ação; `change:override` é negado quando <see cref="DenyOverride"/>.</summary>
    private sealed class OverrideDecider : IEnvironmentAuthorizationDecider
    {
        public bool DenyOverride { get; set; }

        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(DenyOverride && scope == EnvironmentResourceScopes.ChangeOverride
                ? new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Denied, "denied")
                : new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null));
    }

    private sealed class InMemoryRegistrations : IEnvironmentRegistrationStore
    {
        private readonly List<EnvironmentRegistration> all = [];

        public void Add(EnvironmentRegistration registration) => all.Add(registration);

        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentRegistrationPage(all, null));

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult(all.FirstOrDefault(item => item.EnvironmentId == environmentId));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
