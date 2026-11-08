using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T014 com PostgreSQL real: a página do pedido pendente é gravada, com a imagem local da referência; pedido
/// antigo ou vencido é descartado; resultado fora do contrato recusa a página inteira.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageUpdateResultTests : IAsyncLifetime
{
    private const string Reference = "docker.io/library/nginx:1.28";
    private static readonly string Remote = "sha256:" + new string('b', 64);
    private static readonly string LocalImage = "sha256:" + new string('c', 64);
    private readonly Guid environmentId = Guid.NewGuid();
    private readonly Guid requestId = Guid.NewGuid();
    private readonly ImageUpdateSchedulerTests.ManualTime time = new(DateTimeOffset.UtcNow);
    private ControlPlaneDbContext? context;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t025r-{environmentId:N}"[..20], "host-t025r", true, [EnvironmentResourceScopes.Read]), Ct);
        await Store.EnsureScheduledAsync([environmentId], time.Now, Ct);
        Assert.True(await Store.TryAcquireAsync(environmentId, "replica-a", time.Now, Ct));
        Assert.True(await Store.MarkRequestedAsync(environmentId, "replica-a", requestId, time.Now, time.Now + ImageUpdateLimits.Interval, Ct));
    }

    [Fact]
    public async Task PendingRequestPage_IsStored_WithTheLocalImageOfTheReference()
    {
        var outcome = await Ingestion().ApplyAsync(environmentId, Page(requestId, Result(ImageUpdateState.UpdateAvailable)), Ct);

        Assert.Equal(ImageUpdatePageOutcome.Applied, outcome);
        var stored = Assert.Single(await Store.ListAsync([environmentId], Ct));
        Assert.Equal((Reference, ImageUpdateStates.UpdateAvailable, Remote, LocalImage), (stored.Reference, stored.State, stored.RemoteDigest, stored.LocalImageId));
        Assert.Null((await Store.GetScheduleAsync(environmentId, Ct))!.PendingRequestId);
    }

    [Fact]
    public async Task AppliedPage_CountsByStateAndReason_AndTheGaugeHasOnlyTheHost()
    {
        // Spec 025 T026: nenhuma referência, registro ou digest nas dimensões.
        using var metrics = new ImageUpdateMetrics();
        var measured = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter)) l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            lock (measured) measured.Add((instrument.Name, value, tags.ToArray()));
        });
        listener.Start();
        var page = Page(requestId,
            Result(ImageUpdateState.UpdateAvailable),
            Result(ImageUpdateState.UpdateAvailable, platform: "linux/arm64"),
            Result(ImageUpdateState.Failed, reason: ImageUpdateReasons.Timeout, reference: "quay.io/app/api:2"));

        Assert.Equal(ImageUpdatePageOutcome.Applied, await new ImageUpdateResultIngestion(Store, new LocalImages(), time, metrics).ApplyAsync(environmentId, page, Ct));
        listener.RecordObservableInstruments();

        long Sum(string name, string key, string value) =>
            measured.Where(m => m.Name == name && m.Tags.Any(t => t.Key == key && Equals(t.Value, value))).Sum(m => m.Value);
        Assert.Equal(2, Sum("dokpod.image_update.checks", "image_update.state", ImageUpdateStates.UpdateAvailable));
        Assert.Equal(1, Sum("dokpod.image_update.checks", "image_update.reason", ImageUpdateReasons.Timeout));
        Assert.Equal(1, Sum("dokpod.image_update.outdated", "environment.id", environmentId.ToString("D")));
        var keys = measured.SelectMany(m => m.Tags.Select(t => t.Key)).ToHashSet();
        Assert.Subset(new HashSet<string> { "image_update.state", "image_update.reason", "environment.id" }, keys);
        Assert.DoesNotContain(measured.SelectMany(m => m.Tags), t => t.Value is string text && (text.Contains("nginx", StringComparison.Ordinal) || text.Contains("quay.io", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task OtherOrLateRequest_IsDiscarded()
    {
        Assert.Equal(ImageUpdatePageOutcome.Discarded, await Ingestion().ApplyAsync(environmentId, Page(Guid.NewGuid(), Result(ImageUpdateState.UpToDate)), Ct));

        time.Now += ImageUpdateLimits.ResultDeadline + TimeSpan.FromSeconds(1);
        Assert.Equal(ImageUpdatePageOutcome.Discarded, await Ingestion().ApplyAsync(environmentId, Page(requestId, Result(ImageUpdateState.UpToDate)), Ct));
        Assert.Empty(await Store.ListAsync([environmentId], Ct));
    }

    public static TheoryData<string> InvalidResults => new() { "unspecified", "reason_on_success", "bad_platform", "bad_digest", "no_remote", "reason_of_other_state", "digest_only_long" };

    [Theory]
    [MemberData(nameof(InvalidResults))]
    public async Task ResultOutsideTheContract_RejectsTheWholePage(string invalid)
    {
        var bad = invalid switch
        {
            "unspecified" => Result(ImageUpdateState.Unspecified),
            "reason_on_success" => Result(ImageUpdateState.UpToDate, reason: ImageUpdateReasons.Timeout),
            "bad_platform" => Result(ImageUpdateState.UpToDate, platform: "Linux/AMD64"),
            "bad_digest" => Result(ImageUpdateState.UpToDate, remote: "sha256:XYZ"),
            "no_remote" => Result(ImageUpdateState.UpdateAvailable, remote: string.Empty),
            "reason_of_other_state" => Result(ImageUpdateState.Failed, reason: ImageUpdateReasons.LocalImage),
            _ => Result(ImageUpdateState.Failed, reason: ImageUpdateReasons.Timeout, reference: "registro.exemplo.com/" + new string('a', 600) + ":1"),
        };
        var page = Page(requestId, Result(ImageUpdateState.UpToDate), bad);

        Assert.Equal(ImageUpdatePageOutcome.Rejected, await Ingestion().ApplyAsync(environmentId, page, Ct));
        Assert.Empty(await Store.ListAsync([environmentId], Ct));
        Assert.Equal(requestId, (await Store.GetScheduleAsync(environmentId, Ct))!.PendingRequestId);
    }

    private ImageUpdateStore Store => new(context!);

    private ImageUpdateResultIngestion Ingestion() => new(Store, new LocalImages(), time);

    private ImageUpdateCheckResultPage Page(Guid id, params ImageUpdateCheckResult[] results)
    {
        var page = new ImageUpdateCheckResultPage { RequestId = id.ToString("D"), IsLastPage = true };
        page.Results.AddRange(results);
        return page;
    }

    private ImageUpdateCheckResult Result(ImageUpdateState state, string? reason = null, string platform = "linux/amd64", string? remote = null, string reference = Reference) => new()
    {
        Reference = reference,
        Platform = platform,
        State = state,
        Reason = reason ?? string.Empty,
        RemoteDigest = remote ?? Remote,
        CheckedAt = Timestamp.FromDateTimeOffset(time.Now),
    };

    public async ValueTask DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
    }

    private sealed class LocalImages : IImageUpdateTargetReader
    {
        public Task<IReadOnlyList<ImageUpdateTarget>> ListAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageUpdateTarget>>([]);

        public Task<IReadOnlyDictionary<string, string>> LocalImageIdsAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { [Reference] = LocalImage });

        public Task<IReadOnlyList<ImageUpdateContainer>> ListContainersAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageUpdateContainer>>([]);
    }
}
