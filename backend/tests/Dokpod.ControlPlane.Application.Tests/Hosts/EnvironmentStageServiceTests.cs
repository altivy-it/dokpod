using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Hosts;

public sealed class EnvironmentStageServiceTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly AuthenticatedActor Actor = AuthenticatedActor.FromSubject("u-1");

    [Fact]
    public async Task Change_AuditsPreviousAndNewStageAfterTheChange()
    {
        var fixture = new Fixture(EnvironmentStage.Staging);

        var result = await fixture.Service.ChangeAsync(EnvironmentId, EnvironmentStage.Production, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(StageChangeOutcome.Changed, result.Outcome);
        Assert.Equal(EnvironmentStage.Staging, result.Previous);
        var audit = Assert.Single(fixture.Audit.Events);
        Assert.Equal("environment.stage_changed", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("staging", audit.Details!["previousStage"]);
        Assert.Equal("production", audit.Details["stage"]);
        Assert.Equal([EnvironmentId], fixture.Notifier.Notified);
    }

    [Fact]
    public async Task Change_ClearingStageRecordsNone()
    {
        var fixture = new Fixture(EnvironmentStage.Development);

        await fixture.Service.ChangeAsync(EnvironmentId, null, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal("none", Assert.Single(fixture.Audit.Events).Details!["stage"]);
    }

    [Fact]
    public async Task Change_SameStageIsIdempotentWithoutAuditOrNotification()
    {
        var fixture = new Fixture(EnvironmentStage.Production);

        var result = await fixture.Service.ChangeAsync(EnvironmentId, EnvironmentStage.Production, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(StageChangeOutcome.Unchanged, result.Outcome);
        Assert.Empty(fixture.Audit.Events);
        Assert.Empty(fixture.Notifier.Notified);
        Assert.Equal(0, fixture.Stages.Calls);
    }

    [Theory]
    [InlineData(AuthorizationDecisionOutcome.Denied, StageChangeOutcome.Denied, AuditOutcome.Denied)]
    [InlineData(AuthorizationDecisionOutcome.Indeterminate, StageChangeOutcome.Unavailable, AuditOutcome.Indeterminate)]
    public async Task Change_DeniedOrIndeterminateIsAuditedAndDoesNotRevealExistence(
        AuthorizationDecisionOutcome decision, StageChangeOutcome expected, AuditOutcome audited)
    {
        var fixture = new Fixture(stage: null, exists: false, decision: new EnvironmentAuthorizationDecision(decision, "authorization_denied"));

        var result = await fixture.Service.ChangeAsync(EnvironmentId, EnvironmentStage.Production, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(audited, Assert.Single(fixture.Audit.Events).Outcome);
        Assert.Equal(0, fixture.Registrations.Reads);
    }

    [Fact]
    public async Task Change_MissingEnvironmentAfterAuthorizationIsNotFoundWithoutAudit()
    {
        var fixture = new Fixture(stage: null, exists: false);

        var result = await fixture.Service.ChangeAsync(EnvironmentId, EnvironmentStage.Production, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(StageChangeOutcome.NotFound, result.Outcome);
        Assert.Empty(fixture.Audit.Events);
    }

    private sealed class Fixture
    {
        public Fixture(EnvironmentStage? stage, bool exists = true, EnvironmentAuthorizationDecision? decision = null)
        {
            Registrations = new RegistrationStore(exists ? EnvironmentRegistration.Create(EnvironmentId, "srv-prod-01", "srv-prod-01.local", true, ["environment:read"], stage) : null);
            Stages = new StageStore(stage, exists);
            Service = new EnvironmentStageService(
                Registrations,
                Stages,
                new Decider(decision ?? new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null)),
                Audit,
                Notifier);
        }

        public RegistrationStore Registrations { get; }
        public StageStore Stages { get; }
        public AuditWriter Audit { get; } = new();
        public Notifier Notifier { get; } = new();
        public EnvironmentStageService Service { get; }
    }

    private sealed class RegistrationStore(EnvironmentRegistration? registration) : IEnvironmentRegistrationStore
    {
        public int Reads { get; private set; }

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(registration);
        }

        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StageStore(EnvironmentStage? stage, bool exists) : IEnvironmentStageStore
    {
        public int Calls { get; private set; }

        public Task<(bool Found, EnvironmentStage? Previous)> SetStageAsync(Guid environmentId, EnvironmentStage? newStage, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult((exists, stage));
        }
    }

    private sealed class Decider(EnvironmentAuthorizationDecision decision) : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            Assert.Equal("environment:manage", scope);
            return Task.FromResult(decision);
        }
    }

    private sealed class AuditWriter : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class Notifier : IControlPlaneNotifier
    {
        public List<Guid> Notified { get; } = [];

        public Task NotifyAsync(Guid environmentId, string kind, CancellationToken cancellationToken)
        {
            Notified.Add(environmentId);
            return Task.CompletedTask;
        }
    }
}
