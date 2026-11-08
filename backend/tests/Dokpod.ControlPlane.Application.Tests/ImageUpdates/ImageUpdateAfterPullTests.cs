using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.ImageUpdates;

/// <summary>Spec 025 T019: o pull concluído adianta a verificação, sem o limite manual e sem tocar no container (FR-004).</summary>
public sealed class ImageUpdateAfterPullTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("25252525-2525-2525-2525-252525252525");
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PullSucceeded_RequestsCheckNowWithoutTheManualLimit()
    {
        var store = new RecordingStore();
        var observer = new ImageUpdateAfterPull(store, new Entitlement(true), new FixedTime(Now));

        await observer.OnPullSucceededAsync(EnvironmentId, CancellationToken.None);
        await observer.OnPullSucceededAsync(EnvironmentId, CancellationToken.None);

        Assert.Equal([(EnvironmentId, Now, false), (EnvironmentId, Now, false)], store.Requests);
    }

    [Fact]
    public async Task PullSucceeded_WithoutCapabilityInLicense_DoesNothing()
    {
        var store = new RecordingStore();
        var observer = new ImageUpdateAfterPull(store, new Entitlement(false), new FixedTime(Now));

        await observer.OnPullSucceededAsync(EnvironmentId, CancellationToken.None);

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task PullSucceeded_NeverTouchesContainers()
    {
        // FR-004: nenhum comando de criação, recriação ou atualização de container, e o container mantém o image_id.
        Assert.DoesNotContain(
            typeof(ImageUpdateAfterPull).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(IAgentCommandStore) || parameter.ParameterType == typeof(AgentCommandQueueService));
        var store = new RecordingStore();
        var before = store.Checks.ToArray();
        var observer = new ImageUpdateAfterPull(store, new Entitlement(true), new FixedTime(Now));

        await observer.OnPullSucceededAsync(EnvironmentId, CancellationToken.None);

        Assert.Equal(before, store.Checks);
        Assert.Equal("sha256:" + new string('a', 64), Assert.Single(store.Checks).LocalImageId);
        Assert.Equal(["RequestNowAsync"], store.Calls);
    }

    [Fact]
    public async Task PullSucceeded_RejectsEmptyEnvironment()
    {
        var observer = new ImageUpdateAfterPull(new RecordingStore(), new Entitlement(true), new FixedTime(Now));

        await Assert.ThrowsAsync<ArgumentException>(() => observer.OnPullSucceededAsync(Guid.Empty, CancellationToken.None));
    }

    private sealed class Entitlement(bool listed) : IImageUpdateEntitlement
    {
        public Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) => Task.FromResult(listed);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingStore : IImageUpdateStore
    {
        public List<(Guid EnvironmentId, DateTimeOffset Now, bool Manual)> Requests { get; } = [];

        public List<string> Calls { get; } = [];

        public List<ImageUpdateCheck> Checks { get; } =
        [
            new(EnvironmentId, "docker.io/library/nginx:1.27", "linux/amd64", "sha256:" + new string('a', 64), ImageUpdateStates.UpdateAvailable, null,
                "sha256:" + new string('b', 64), Now.AddHours(-1), Now.AddHours(-1), Now.AddDays(-3)),
        ];

        public Task<ImageUpdateManualRequest> RequestNowAsync(Guid environmentId, DateTimeOffset nowUtc, bool manual, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(RequestNowAsync));
            Requests.Add((environmentId, nowUtc, manual));
            return Task.FromResult(new ImageUpdateManualRequest(ImageUpdateManualOutcome.Accepted, nowUtc, null));
        }

        public Task EnsureScheduledAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Unexpected();

        public Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken) => Unexpected<IReadOnlyList<Guid>>();

        public Task<bool> TryAcquireAsync(Guid environmentId, string owner, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Unexpected<bool>();

        public Task<bool> MarkRequestedAsync(
            Guid environmentId, string owner, Guid requestId, DateTimeOffset nowUtc, DateTimeOffset nextDueAtUtc, CancellationToken cancellationToken) => Unexpected<bool>();

        public Task ReleaseAsync(Guid environmentId, string owner, CancellationToken cancellationToken) => Unexpected();

        public Task<ImageUpdateApplyOutcome> ApplyPageAsync(
            Guid environmentId, Guid requestId, IReadOnlyList<ImageUpdateResult> results, bool isLastPage, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Unexpected<ImageUpdateApplyOutcome>();

        public Task UpsertLocalAsync(Guid environmentId, IReadOnlyList<ImageUpdateResult> results, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Unexpected();

        public Task<IReadOnlyList<ImageUpdateCheck>> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Unexpected<IReadOnlyList<ImageUpdateCheck>>();

        public Task<IReadOnlyList<ImageUpdateCheck>> ListAsync(IReadOnlyCollection<Guid> environmentIds, IReadOnlyCollection<string> references, CancellationToken cancellationToken) =>
            Unexpected<IReadOnlyList<ImageUpdateCheck>>();

        public Task<ImageUpdateScheduleState?> GetScheduleAsync(Guid environmentId, CancellationToken cancellationToken) => Unexpected<ImageUpdateScheduleState?>();

        private Task Unexpected([System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            Calls.Add(name);
            return Task.CompletedTask;
        }

        private Task<T> Unexpected<T>([System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            Calls.Add(name);
            return Task.FromResult(default(T)!);
        }
    }
}
