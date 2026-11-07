using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Application.Registries;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T013, com a agenda no PostgreSQL real e relógio injetável: lease por host, itens do inventário, política do
/// estágio, agente sem capability, host desconectado e licença sem <c>image-update-notice</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageUpdateSchedulerTests : IAsyncLifetime
{
    private readonly Guid environmentId = Guid.NewGuid();
    private readonly ManualTime time = new(DateTimeOffset.UtcNow);
    private readonly FakeChannel channel = new();
    private ControlPlaneDbContext? context;
    private EnvironmentRegistration? host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        host = EnvironmentRegistration.Create(environmentId, $"t025s-{environmentId:N}"[..20], "host-t025s", true, [EnvironmentResourceScopes.Read]);
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(host, Ct);
    }

    [Fact]
    public async Task DueHost_SendsTheAllowedReferences_AndRecordsTheOthersLocally()
    {
        channel.Connected = channel.Supported = true;

        var sent = await Scheduler().RunOnceAsync("replica-a", Ct);

        Assert.Equal(1, sent);
        var (target, request) = Assert.Single(channel.Sent);
        Assert.Equal(environmentId, target);
        Assert.Equal(["docker.io/library/nginx:1.28"], request.Items.Select(i => i.Reference));
        var stored = Assert.Single(await Store.ListAsync([environmentId], Ct));
        Assert.Equal(("quay.io/app/api:2", ImageUpdateStates.NotCheckable, ImageUpdateReasons.RegistryNotAllowed), (stored.Reference, stored.State, stored.Reason));
        var schedule = (await Store.GetScheduleAsync(environmentId, Ct))!;
        Assert.Equal(Guid.Parse(request.RequestId), schedule.PendingRequestId);
        Assert.InRange(schedule.NextDueAtUtc, time.Now + TimeSpan.FromMinutes(323), time.Now + ImageUpdateLimits.Interval);

        // Outra réplica no mesmo instante não pede de novo: o host não está mais vencido.
        Assert.Equal(0, await Scheduler().RunOnceAsync("replica-b", Ct));
        Assert.Single(channel.Sent);
    }

    [Fact]
    public async Task AgentWithoutTheCapability_IsAgentUnsupported_WithoutAnyRequest()
    {
        channel.Connected = true;

        Assert.Equal(0, await Scheduler().RunOnceAsync("replica-a", Ct));

        Assert.Empty(channel.Sent);
        var states = (await Store.ListAsync([environmentId], Ct)).ToDictionary(c => c.Reference, c => c.Reason);
        Assert.Equal(ImageUpdateReasons.AgentUnsupported, states["docker.io/library/nginx:1.28"]);
        Assert.Equal(ImageUpdateReasons.RegistryNotAllowed, states["quay.io/app/api:2"]);
        Assert.DoesNotContain(environmentId, await Store.ListDueAsync(time.Now, 1000, Ct));
    }

    [Fact]
    public async Task DisconnectedHost_StaysDue()
    {
        Assert.Equal(0, await Scheduler().RunOnceAsync("replica-a", Ct));

        Assert.Empty(channel.Sent);
        Assert.Contains(environmentId, await Store.ListDueAsync(time.Now, 1000, Ct));
        Assert.Null((await Store.GetScheduleAsync(environmentId, Ct))!.LeaseOwner);
    }

    [Fact]
    public async Task FailedSend_ReleasesTheHost_WhichStaysDue()
    {
        channel.Connected = channel.Supported = true;
        channel.Accepts = false;

        Assert.Equal(0, await Scheduler().RunOnceAsync("replica-a", Ct));

        Assert.Contains(environmentId, await Store.ListDueAsync(time.Now, 1000, Ct));
        Assert.Null((await Store.GetScheduleAsync(environmentId, Ct))!.PendingRequestId);
    }

    [Fact]
    public async Task LicenseWithoutTheCapability_SchedulesNothing()
    {
        channel.Connected = channel.Supported = true;

        Assert.Equal(0, await Scheduler(licensed: false).RunOnceAsync("replica-a", Ct));

        Assert.Empty(channel.Sent);
        Assert.Null(await Store.GetScheduleAsync(environmentId, Ct));
    }

    private ImageUpdateStore Store => new(context!);

    private ImageUpdateScheduler Scheduler(bool licensed = true) => new(
        Store,
        new FakeTargets(),
        channel,
        new FakeEntitlement(licensed),
        new SingleRegistration(host!),
        new NoStoredPolicies(),
        new ImagePullPolicy { AllowedRegistriesByStage = { ["none"] = ["docker.io"] } },
        time,
        () => 0.5);

    public async ValueTask DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
    }

    // Começa em microssegundo inteiro, a precisão do timestamptz: o valor relido do PostgreSQL volta igual ao gravado.
    internal sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeChannel : IImageUpdateAgentChannel
    {
        public bool Connected { get; set; }

        public bool Supported { get; set; }

        public bool Accepts { get; set; } = true;

        public List<(Guid, ImageUpdateCheckRequest)> Sent { get; } = [];

        public bool IsConnected(Guid environmentId) => Connected;

        public bool Supports(Guid environmentId) => Supported;

        public Task<bool> SendAsync(Guid environmentId, ImageUpdateCheckRequest request, CancellationToken cancellationToken)
        {
            if (Accepts) Sent.Add((environmentId, request));
            return Task.FromResult(Accepts);
        }
    }

    private sealed class FakeTargets : IImageUpdateTargetReader
    {
        public Task<IReadOnlyList<ImageUpdateTarget>> ListAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageUpdateTarget>>(
            [
                new("docker.io/library/nginx:1.28", "sha256:" + new string('c', 64), "linux/amd64"),
                new("quay.io/app/api:2", "sha256:" + new string('d', 64), "linux/amd64"),
            ]);

        public Task<IReadOnlyDictionary<string, string>> LocalImageIdsAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<IReadOnlyList<ImageUpdateContainer>> ListContainersAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageUpdateContainer>>([]);
    }

    private sealed class FakeEntitlement(bool licensed) : IImageUpdateEntitlement
    {
        public Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) => Task.FromResult(licensed);
    }

    private sealed class SingleRegistration(EnvironmentRegistration registration) : IEnvironmentRegistrationStore
    {
        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentRegistrationPage(afterEnvironmentId is null ? [registration] : [], null));

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult<EnvironmentRegistration?>(registration);
    }

    private sealed class NoStoredPolicies : IRegistryPolicyStore
    {
        public Task<IReadOnlyList<StoredRegistryPolicy>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StoredRegistryPolicy>>([]);

        public Task SaveAsync(StoredRegistryPolicy policy, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
