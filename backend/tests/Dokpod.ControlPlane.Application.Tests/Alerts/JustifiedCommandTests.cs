using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Alerts;

/// <summary>Spec 016 T006: justificativa de parar, pausar e remover no envio de comandos.</summary>
public sealed class JustifiedCommandTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ContainerId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(AgentCommandKind.StopContainer)]
    [InlineData(AgentCommandKind.PauseContainer)]
    [InlineData(AgentCommandKind.DeleteContainer)]
    public async Task Production_WithoutJustification_IsRejectedBeforeEnqueue(AgentCommandKind kind)
    {
        var (service, store, justifications) = Create(EnvironmentStage.Production, AgentCommandEnqueueResult.Created);

        var result = await Submit(service, kind, null);

        Assert.Equal(ContainerCommandSubmissionOutcome.JustificationRequired, result.Outcome);
        Assert.Equal("justification_required", result.FailureCode);
        Assert.Null(store.Command);
        Assert.Empty(justifications.Recorded);
    }

    [Fact]
    public async Task Production_RestartDoesNotNeedJustification()
    {
        var (service, _, justifications) = Create(EnvironmentStage.Production, AgentCommandEnqueueResult.Created);

        var result = await Submit(service, AgentCommandKind.RestartContainer, null);

        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, result.Outcome);
        Assert.Empty(justifications.Recorded);
    }

    [Fact]
    public async Task Development_WithoutJustification_IsAcceptedWithoutRecord()
    {
        var (service, _, justifications) = Create(EnvironmentStage.Development, AgentCommandEnqueueResult.Created);

        var result = await Submit(service, AgentCommandKind.StopContainer, "   ");

        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, result.Outcome);
        Assert.Empty(justifications.Recorded);
    }

    [Fact]
    public async Task CommandAudit_AlwaysTargetsTheContainer()
    {
        var (service, store, _) = Create(EnvironmentStage.Development, AgentCommandEnqueueResult.Created);

        await Submit(service, AgentCommandKind.RestartContainer, null);

        Assert.Equal(ContainerId, store.AuditEvent!.SubjectId);
        Assert.Null(store.AuditEvent.Details);
    }

    [Fact]
    public async Task ShortJustification_IsInvalidEvenWhenOptional()
    {
        var (service, store, _) = Create(EnvironmentStage.Development, AgentCommandEnqueueResult.Created);

        var result = await Submit(service, AgentCommandKind.StopContainer, "curta");

        Assert.Equal(ContainerCommandSubmissionOutcome.JustificationInvalid, result.Outcome);
        Assert.Null(store.Command);
    }

    [Theory]
    [InlineData(AgentCommandEnqueueResult.Created, ContainerCommandSubmissionOutcome.Accepted)]
    [InlineData(AgentCommandEnqueueResult.Duplicate, ContainerCommandSubmissionOutcome.Duplicate)]
    public async Task ValidJustification_IsMaskedAuditedAndRecordedOutsideThePayload(AgentCommandEnqueueResult enqueue, ContainerCommandSubmissionOutcome expected)
    {
        var (service, store, justifications) = Create(EnvironmentStage.Production, enqueue);

        var result = await Submit(service, AgentCommandKind.StopContainer, "  Janela de manutenção " + "pass" + "word=segredo123  ");

        Assert.Equal(expected, result.Outcome);
        var recorded = Assert.Single(justifications.Recorded);
        Assert.Equal(JustifiedAction.Stop, recorded.Action);
        Assert.Equal(result.CommandId, recorded.CommandId);
        Assert.Equal("user-1", recorded.Actor);
        Assert.DoesNotContain("segredo123", recorded.Reason);
        Assert.StartsWith("Janela de manutenção", recorded.Reason);
        Assert.Equal(recorded.Reason, store.AuditEvent!.Details!["justification"]);
    }

    [Fact]
    public async Task ConflictingPayload_DoesNotRecordJustification()
    {
        var (service, _, justifications) = Create(EnvironmentStage.Production, AgentCommandEnqueueResult.ConflictingPayload);

        var result = await Submit(service, AgentCommandKind.DeleteContainer, "Remoção planejada do serviço antigo");

        Assert.Equal(ContainerCommandSubmissionOutcome.ConflictingPayload, result.Outcome);
        Assert.Empty(justifications.Recorded);
    }

    private static Task<ContainerCommandSubmissionResult> Submit(ContainerCommandService service, AgentCommandKind kind, string? justification) =>
        service.SubmitAsync(
            EnvironmentId,
            Guid.NewGuid(),
            kind,
            ContainerId,
            "revision-01",
            DateTimeOffset.UtcNow.AddMinutes(1),
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken,
            justification);

    private static (ContainerCommandService Service, Store Store, Justifications Justifications) Create(EnvironmentStage stage, AgentCommandEnqueueResult enqueue)
    {
        var store = new Store(enqueue);
        var justifications = new Justifications();
        var service = new ContainerCommandService(
            new Decider(AuthorizationDecisionOutcome.Allowed),
            new Registrations(stage),
            new Sessions(new AgentSession(EnvironmentId, Guid.NewGuid(), 17)),
            new AgentCommandQueueService(store, new Delivery(), TimeProvider.System),
            justifications);
        return (service, store, justifications);
    }

    internal sealed class Justifications : IActionJustificationStore
    {
        public List<ContainerActionJustification> Recorded { get; } = [];

        public Task<IReadOnlyDictionary<EnvironmentStage, bool>> GetPoliciesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<EnvironmentStage, bool>>(new Dictionary<EnvironmentStage, bool>
            {
                [EnvironmentStage.Production] = true,
                [EnvironmentStage.Staging] = false,
                [EnvironmentStage.Development] = false,
            });

        public Task<IReadOnlyList<JustificationPolicyEntry>> ListPolicyEntriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JustificationPolicyEntry>>(
            [
                new(EnvironmentStage.Production, true, "admin", DateTimeOffset.UnixEpoch),
                new(EnvironmentStage.Staging, false, null, null),
                new(EnvironmentStage.Development, false, null, null),
            ]);

        public Task RecordAsync(ContainerActionJustification justification, CancellationToken cancellationToken)
        {
            Recorded.Add(justification);
            return Task.CompletedTask;
        }

        public Task SetPolicyAsync(EnvironmentStage stage, bool required, string actor, DateTimeOffset at, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<(Guid EnvironmentId, string ContainerId), ContainerActionJustification>> GetCurrentAsync(IReadOnlyCollection<(Guid EnvironmentId, string ContainerId)> containers, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContainerActionJustification>> ListAsync(Guid environmentId, string? containerId, DateTimeOffset? sinceUtc, DateTimeOffset? untilUtc, JustificationCursor? after, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContainerActionJustification>> ListOpenAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task EndAsync(Guid justificationId, DateTimeOffset at, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasRecentAsync(Guid environmentId, string containerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
