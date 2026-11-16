using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Agents;

/// <summary>Spec 033 T013 (FR-024, FR-025, D-004): cadastro e identidade pelo instalador, idempotentes e auditados.</summary>
public sealed class AgentIdentityRegistrationServiceTests
{
    private static readonly string Fingerprint = new('a', 64);
    private static readonly InstallerOperator Operator = new("Ana Operadora", "instalação inicial do host local");

    private readonly InMemoryEnvironments environments = new();
    private readonly InMemoryIdentities identities = new();
    private readonly RecordingAudit audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AgentIdentityRegistrationService Service() => new(environments, identities, identities, audit);

    [Fact]
    public async Task RegisterEnvironment_CreatesOnce_AuditsAsInstaller_AndRepeatsWithoutChange()
    {
        var registration = Registration(Guid.NewGuid());

        var first = await Service().RegisterEnvironmentAsync(registration, Operator, Guid.NewGuid(), Ct);
        var again = await Service().RegisterEnvironmentAsync(registration, Operator, Guid.NewGuid(), Ct);

        Assert.True(first is { Changed: true, Succeeded: true });
        Assert.True(again is { Changed: false, Succeeded: true });
        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal("environment.register", auditEvent.Action);
        Assert.Equal(AuditActorKind.System, auditEvent.ActorKind);
        Assert.Equal("installer", auditEvent.ActorId);
        Assert.Equal("Ana Operadora", auditEvent.ActorName);
        Assert.Equal("instalação inicial do host local", auditEvent.Details!["justification"]);
        Assert.Equal(AuditOutcome.Succeeded, auditEvent.Outcome);
    }

    [Fact]
    public async Task RegisterEnvironment_WithDifferentData_IsRefusedAndAudited()
    {
        var id = Guid.NewGuid();
        await Service().RegisterEnvironmentAsync(Registration(id), Operator, Guid.NewGuid(), Ct);

        var result = await Service().RegisterEnvironmentAsync(Registration(id, name: "outro-nome"), Operator, Guid.NewGuid(), Ct);

        Assert.Equal("environment_conflict", result.FailureCode);
        Assert.Equal("host-local", (await environments.GetAsync(id, Ct))!.Name);
        Assert.Equal(AuditOutcome.Failed, audit.Events[^1].Outcome);
        Assert.Equal("environment_conflict", audit.Events[^1].FailureCode);
    }

    [Fact]
    public async Task RegisterIdentity_IsIdempotent_AndAuditedWithTheNewAction()
    {
        var id = Guid.NewGuid();
        await Service().RegisterEnvironmentAsync(Registration(id), Operator, Guid.NewGuid(), Ct);

        var first = await Service().RegisterIdentityAsync(id, "aa:" + Fingerprint[2..], Operator, Guid.NewGuid(), Ct);
        var again = await Service().RegisterIdentityAsync(id, Fingerprint.ToUpperInvariant(), Operator, Guid.NewGuid(), Ct);

        Assert.True(first is { Changed: true, Succeeded: true });
        Assert.True(again is { Changed: false, Succeeded: true });
        Assert.Equal(id, identities.Bound[Fingerprint.ToUpperInvariant()]);
        var registered = Assert.Single(audit.Events, item => item.Action == AgentIdentityRegistrationService.RegisterAction);
        Assert.Equal(Fingerprint.ToUpperInvariant(), registered.SubjectId);
        Assert.Contains(AgentIdentityRegistrationService.RegisterAction, AuditCategories.Actions["environments"]);
    }

    [Fact]
    public async Task RegisterIdentity_BoundToAnotherEnvironmentOrRevoked_IsRefused()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await Service().RegisterEnvironmentAsync(Registration(a), Operator, Guid.NewGuid(), Ct);
        await Service().RegisterEnvironmentAsync(Registration(b), Operator, Guid.NewGuid(), Ct);
        await Service().RegisterIdentityAsync(a, Fingerprint, Operator, Guid.NewGuid(), Ct);

        var other = await Service().RegisterIdentityAsync(b, Fingerprint, Operator, Guid.NewGuid(), Ct);
        await Service().RevokeIdentityAsync(a, Operator, Guid.NewGuid(), Ct);
        var revoked = await Service().RegisterIdentityAsync(a, Fingerprint, Operator, Guid.NewGuid(), Ct);

        Assert.Equal("agent_identity_bound_to_another_environment", other.FailureCode);
        Assert.Equal("agent_identity_revoked", revoked.FailureCode);
        Assert.Equal(2, audit.Events.Count(item => item is { Action: AgentIdentityRegistrationService.RegisterAction, Outcome: AuditOutcome.Failed }));
    }

    [Fact]
    public async Task RegisterIdentity_ForAnUnknownEnvironment_ChangesNothing()
    {
        var result = await Service().RegisterIdentityAsync(Guid.NewGuid(), Fingerprint, Operator, Guid.NewGuid(), Ct);

        Assert.Equal("environment_not_found", result.FailureCode);
        Assert.Empty(identities.Bound);
        Assert.Empty(audit.Events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zz00000000000000000000000000000000000000000000000000000000000000")]
    public async Task RegisterIdentity_WithAnInvalidFingerprint_IsRejected(string fingerprint) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service().RegisterIdentityAsync(Guid.NewGuid(), fingerprint, Operator, Guid.NewGuid(), Ct));

    [Fact]
    public async Task RevokeIdentity_UsesTheEndpointAction_AndRepeatsWithoutChange()
    {
        var id = Guid.NewGuid();
        await Service().RegisterEnvironmentAsync(Registration(id), Operator, Guid.NewGuid(), Ct);
        await Service().RegisterIdentityAsync(id, Fingerprint, Operator, Guid.NewGuid(), Ct);

        var first = await Service().RevokeIdentityAsync(id, Operator, Guid.NewGuid(), Ct);
        var again = await Service().RevokeIdentityAsync(id, Operator, Guid.NewGuid(), Ct);

        Assert.True(first.Changed);
        Assert.False(again.Changed);
        Assert.Single(audit.Events, item => item.Action == "environment.revoke");
    }

    private static EnvironmentRegistration Registration(Guid id, string name = "host-local") =>
        EnvironmentRegistration.Create(id, name, "host-local.lab", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production);

    private sealed class InMemoryEnvironments : IEnvironmentRegistrationStore
    {
        private readonly Dictionary<Guid, EnvironmentRegistration> items = [];

        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentRegistrationPage(items.Values.ToArray(), null));

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) =>
            Task.FromResult(items.TryAdd(registration.EnvironmentId, registration)
                ? EnvironmentRegistrationStoreResult.Created
                : EnvironmentRegistrationStoreResult.AlreadyExists);

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult(items.GetValueOrDefault(environmentId));
    }

    private sealed class InMemoryIdentities : IAgentIdentityRegistry, IAgentIdentityEnrollment
    {
        public Dictionary<string, Guid> Bound { get; } = new(StringComparer.Ordinal);
        private readonly HashSet<string> revoked = new(StringComparer.Ordinal);

        public Task<AgentIdentityEnrollmentOutcome> RegisterAsync(Guid environmentId, string certificateFingerprint, CancellationToken cancellationToken)
        {
            if (Bound.TryGetValue(certificateFingerprint, out var existing))
            {
                return Task.FromResult(revoked.Contains(certificateFingerprint) ? AgentIdentityEnrollmentOutcome.Revoked
                    : existing == environmentId ? AgentIdentityEnrollmentOutcome.Unchanged
                    : AgentIdentityEnrollmentOutcome.BoundToAnotherEnvironment);
            }

            Bound[certificateFingerprint] = environmentId;
            return Task.FromResult(AgentIdentityEnrollmentOutcome.Created);
        }

        public ValueTask<AgentIdentity?> FindByFingerprintAsync(string certificateFingerprint, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Bound.TryGetValue(certificateFingerprint, out var id) && !revoked.Contains(certificateFingerprint)
                ? new AgentIdentity(id, certificateFingerprint)
                : null);

        public Task<bool> RevokeEnvironmentAsync(Guid environmentId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
        {
            var active = Bound.Where(pair => pair.Value == environmentId && !revoked.Contains(pair.Key)).Select(pair => pair.Key).ToArray();
            foreach (var fingerprint in active)
            {
                revoked.Add(fingerprint);
            }

            return Task.FromResult(active.Length > 0);
        }
    }

    private sealed class RecordingAudit : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
