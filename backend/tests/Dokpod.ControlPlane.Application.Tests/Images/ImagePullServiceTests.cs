using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Images;

/// <summary>Spec 012 T009/T015: pull por host e suíte negativa do threat model (D-009).</summary>
public sealed class ImagePullServiceTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Pull_AuthorizesImagePullScopeAndEnqueuesNormalizedReferenceWithV2Hash()
    {
        var harness = new Harness();

        var result = await harness.PullAsync("nginx:1.27");

        Assert.Equal(ImagePullOutcome.Accepted, result.Outcome);
        Assert.Equal(EnvironmentResourceScopes.PullImage, harness.Authorization.Scope);
        var command = harness.Store.Command!.Command;
        Assert.Equal(AgentCommandKind.PullImage, command.Kind);
        Assert.Equal(string.Empty, command.ContainerId);
        Assert.Equal("docker.io/library/nginx:1.27", Assert.IsType<PullImageParameters>(command.Parameters).Reference.Canonical);
        Assert.Equal(AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, "", "", command.Parameters), command.PayloadHash);
        Assert.Equal("image.pull", harness.Store.AuditEvent!.Action);
    }

    [Theory]
    [InlineData("user:senha@registry.empresa.com/app:1")]
    [InlineData("registry.empresa.com/app:1?token=x")]
    [InlineData("nginx latest")]
    [InlineData("Nginx")]
    [InlineData("")]
    public async Task Pull_RejectsReferencesOutsideTheGrammarBeforeAuthorizing(string reference)
    {
        var harness = new Harness();

        var result = await harness.PullAsync(reference);

        Assert.Equal(ImagePullOutcome.InvalidReference, result.Outcome);
        Assert.Null(harness.Authorization.Scope);
        Assert.Null(harness.Store.Command);
    }

    [Fact]
    public async Task Pull_DeniesMissingScope()
    {
        var harness = new Harness(outcome: AuthorizationDecisionOutcome.Denied);

        var result = await harness.PullAsync("nginx");

        Assert.Equal(ImagePullOutcome.AuthorizationDenied, result.Outcome);
        Assert.Null(harness.Store.Command);
    }

    [Fact]
    public async Task Pull_RespectsAllowedRegistriesByStage()
    {
        var policy = new ImagePullPolicy { AllowedRegistriesByStage = new(StringComparer.OrdinalIgnoreCase) { ["production"] = ["registry.empresa.com"] } };
        var production = new Harness(stage: EnvironmentStage.Production, policy: policy);
        var development = new Harness(stage: EnvironmentStage.Development, policy: policy);

        Assert.Equal(ImagePullOutcome.RegistryNotAllowed, (await production.PullAsync("nginx")).Outcome);
        Assert.Equal(ImagePullOutcome.Accepted, (await production.PullAsync("registry.empresa.com/pedidos/api:3")).Outcome);
        Assert.Equal(ImagePullOutcome.Accepted, (await development.PullAsync("nginx")).Outcome);
    }

    [Fact]
    public async Task Pull_RequiresConnectedAgentWithCapability13()
    {
        Assert.Equal(ImagePullOutcome.AgentOffline, (await new Harness(connected: false).PullAsync("nginx")).Outcome);
        Assert.Equal(ImagePullOutcome.NotSupported, (await new Harness(capabilities: ["Inventory"]).PullAsync("nginx")).Outcome);
    }

    [Fact]
    public async Task Pull_ReportsConflictWhenIdempotencyKeyCarriesDifferentParameters()
    {
        var harness = new Harness(enqueue: AgentCommandEnqueueResult.ConflictingPayload);

        var result = await harness.PullAsync("nginx:1.28");

        Assert.Equal(ImagePullOutcome.ConflictingPayload, result.Outcome);
        Assert.Equal("command_id_conflict", result.FailureCode);
    }

    [Fact]
    public void Accumulator_AssemblesPagesAndRejectsOutOfOrderPages()
    {
        var accumulator = new ImageSnapshotAccumulator(EnvironmentId, TimeProvider.System);
        var id = Guid.NewGuid().ToString("D");
        var image = new ImageInventoryItem("sha256:" + new string('a', 64), [], [], 1, null, null, null, false);
        var other = image with { ImageId = "sha256:" + new string('b', 64) };

        Assert.Null(accumulator.AddPage(new ImageSnapshotPage(id, 9, 0, false, [image]), out var failure));
        Assert.Null(failure);
        var snapshot = accumulator.AddPage(new ImageSnapshotPage(id, 9, 1, true, [other]), out failure);
        Assert.Null(failure);
        Assert.Equal(2, snapshot!.Images.Count);

        accumulator.AddPage(new ImageSnapshotPage(id, 10, 2, false, [image]), out failure);
        Assert.Equal("image_snapshot_invalid", failure);
    }

    private sealed class Harness(
        AuthorizationDecisionOutcome outcome = AuthorizationDecisionOutcome.Allowed,
        EnvironmentStage? stage = null,
        ImagePullPolicy? policy = null,
        bool connected = true,
        string[]? capabilities = null,
        AgentCommandEnqueueResult enqueue = AgentCommandEnqueueResult.Created)
    {
        public Decider Authorization { get; } = new(outcome);
        public Store Store { get; } = new(enqueue);

        public Task<ImagePullResult> PullAsync(string reference) =>
            new ImagePullService(
                    Authorization,
                    new Registrations(stage),
                    new Sessions(connected ? new AgentSession(EnvironmentId, Guid.NewGuid(), 5) : null),
                    new AgentCommandQueueService(Store, new Delivery(), TimeProvider.System),
                    new FakeHostStore(capabilities ?? ["Inventory", "ImagePull"], EnvironmentId),
                    policy ?? new ImagePullPolicy())
                .PullAsync(EnvironmentId, Guid.NewGuid(), reference, Now.AddMinutes(5).ToUniversalTime(), AuthenticatedActor.FromSubject("user-1"), "token", Guid.NewGuid(), TestContext.Current.CancellationToken);
    }
}
