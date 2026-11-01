using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T006 com PostgreSQL real: agenda e lease por host, limite de "Verificar agora", páginas do pedido pendente e
/// as transições de data-model.md (sem falso "Atualizada" numa falha, SC-002).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageUpdateStoreTests : IAsyncLifetime
{
    private const string Reference = "docker.io/library/nginx:1.28";
    private const string Platform = "linux/amd64";
    private static readonly string OldDigest = "sha256:" + new string('a', 64);
    private static readonly string NewDigest = "sha256:" + new string('b', 64);
    private static readonly string ImageId = "sha256:" + new string('c', 64);
    private readonly Guid environmentId = Guid.NewGuid();
    private ControlPlaneDbContext? context;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ImageUpdateStore Store => new(context!);

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t025-{environmentId:N}"[..20], "host-t025", true, [EnvironmentResourceScopes.Read]), Ct);
    }

    [Fact]
    public async Task DueHost_IsLeasedByOneReplica_AndRescheduledWithinSixHours()
    {
        var now = DateTimeOffset.UtcNow;
        await Store.EnsureScheduledAsync([environmentId], now, Ct);
        await using var other = NetworksIntegrationTests.CreateContextOrSkip();

        Assert.Contains(environmentId, await Store.ListDueAsync(now, 1000, Ct));
        Assert.True(await Store.TryAcquireAsync(environmentId, "replica-a", now, Ct));
        Assert.False(await new ImageUpdateStore(other).TryAcquireAsync(environmentId, "replica-b", now, Ct));
        Assert.DoesNotContain(environmentId, await Store.ListDueAsync(now, 1000, Ct));

        var next = ImageUpdateLimits.NextDue(now, 1);
        Assert.True(await Store.MarkRequestedAsync(environmentId, "replica-a", Guid.NewGuid(), now, next, Ct));
        var schedule = (await Store.GetScheduleAsync(environmentId, Ct))!;
        Assert.Null(schedule.LeaseOwner);
        Assert.InRange(schedule.NextDueAtUtc, now + TimeSpan.FromMinutes(323), now + ImageUpdateLimits.Interval);
        Assert.InRange(ImageUpdateLimits.NextDue(now, 0), now + ImageUpdateLimits.Interval, now + ImageUpdateLimits.Interval);
        Assert.False(await Store.TryAcquireAsync(environmentId, "replica-b", now, Ct));
    }

    [Fact]
    public async Task ExpiredLease_IsTakenOver()
    {
        var now = DateTimeOffset.UtcNow;
        await Store.EnsureScheduledAsync([environmentId], now, Ct);
        Assert.True(await Store.TryAcquireAsync(environmentId, "replica-a", now, Ct));

        Assert.True(await Store.TryAcquireAsync(environmentId, "replica-b", now + ImageUpdateLimits.LeaseDuration, Ct));
        Assert.False(await Store.MarkRequestedAsync(environmentId, "replica-a", Guid.NewGuid(), now, now, Ct));
    }

    [Fact]
    public async Task ManualRequest_IsLimitedToOnePerMinute_AfterPullIsNot()
    {
        var now = DateTimeOffset.UtcNow;

        var first = await Store.RequestNowAsync(environmentId, now, manual: true, Ct);
        var second = await Store.RequestNowAsync(environmentId, now.AddSeconds(30), manual: true, Ct);
        var afterPull = await Store.RequestNowAsync(environmentId, now.AddSeconds(40), manual: false, Ct);
        var third = await Store.RequestNowAsync(environmentId, now.AddMinutes(1), manual: true, Ct);

        Assert.Equal(ImageUpdateManualOutcome.Accepted, first.Outcome);
        Assert.Equal((ImageUpdateManualOutcome.RateLimited, Trim(now + ImageUpdateLimits.ManualCooldown)), (second.Outcome, Trim(second.RetryAtUtc!.Value)));
        Assert.Equal(ImageUpdateManualOutcome.Accepted, afterPull.Outcome);
        Assert.Equal(ImageUpdateManualOutcome.Accepted, third.Outcome);
    }

    [Fact]
    public async Task Transitions_KeepOutdatedSinceAndTheLastGoodResultAcrossFailures()
    {
        var t0 = DateTimeOffset.UtcNow;
        var request = await RequestAsync(t0);
        await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.UpToDate, null, OldDigest)], true, t0, Ct);
        var t1 = t0.AddMinutes(1);
        request = await RequestAsync(t1);
        await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.UpdateAvailable, null, NewDigest)], true, t1, Ct);
        var outdated = await GetAsync();
        Assert.Equal((ImageUpdateStates.UpdateAvailable, Trim(t1)), (outdated.State, Trim(outdated.OutdatedSinceUtc!.Value)));

        var t2 = t1.AddMinutes(1);
        request = await RequestAsync(t2);
        await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.Failed, ImageUpdateReasons.RateLimited, null)], true, t2, Ct);
        var failed = await GetAsync();
        Assert.Equal(
            (ImageUpdateStates.Failed, ImageUpdateReasons.RateLimited, NewDigest, Trim(t1), Trim(t1)),
            (failed.State, failed.Reason, failed.RemoteDigest, Trim(failed.LastSuccessAtUtc!.Value), Trim(failed.OutdatedSinceUtc!.Value)));

        var t3 = t2.AddMinutes(1);
        request = await RequestAsync(t3);
        await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.UpdateAvailable, null, NewDigest)], true, t3, Ct);
        Assert.Equal(Trim(t1), Trim((await GetAsync()).OutdatedSinceUtc!.Value));

        var t4 = t3.AddMinutes(1);
        request = await RequestAsync(t4);
        await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.UpToDate, null, NewDigest)], true, t4, Ct);
        var current = await GetAsync();
        Assert.Equal((ImageUpdateStates.UpToDate, (DateTimeOffset?)null), (current.State, current.OutdatedSinceUtc));
    }

    [Fact]
    public async Task OnlyThePendingRequestWithinTheDeadlineIsApplied_AndTheLastPageDropsUnusedReferences()
    {
        var t0 = DateTimeOffset.UtcNow;
        var request = await RequestAsync(t0);
        await Store.ApplyPageAsync(
            environmentId, request,
            [Result(ImageUpdateStates.UpToDate, null, OldDigest), Result(ImageUpdateStates.UpToDate, null, OldDigest, "docker.io/library/redis:7")],
            true, t0, Ct);

        var t1 = t0.AddMinutes(10);
        request = await RequestAsync(t1);
        Assert.Equal(ImageUpdateApplyOutcome.Discarded, await Store.ApplyPageAsync(environmentId, Guid.NewGuid(), [Result(ImageUpdateStates.UpdateAvailable, null, NewDigest)], true, t1, Ct));
        Assert.Equal(
            ImageUpdateApplyOutcome.Discarded,
            await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.UpdateAvailable, null, NewDigest)], true, t1 + ImageUpdateLimits.ResultDeadline + TimeSpan.FromSeconds(1), Ct));

        await Store.UpsertLocalAsync(environmentId, [Result(ImageUpdateStates.NotCheckable, ImageUpdateReasons.RegistryNotAllowed, null, "registro.interno/app:1")], t1, Ct);
        Assert.Equal(ImageUpdateApplyOutcome.Applied, await Store.ApplyPageAsync(environmentId, request, [Result(ImageUpdateStates.UpdateAvailable, null, NewDigest)], false, t1, Ct));
        Assert.Equal(3, (await Store.ListAsync([environmentId], Ct)).Count);
        Assert.Equal(ImageUpdateApplyOutcome.Applied, await Store.ApplyPageAsync(environmentId, request, [], true, t1, Ct));

        var rows = await Store.ListAsync([environmentId], Ct);
        Assert.Equal(["docker.io/library/nginx:1.28", "registro.interno/app:1"], rows.Select(row => row.Reference).Order());
        Assert.Null((await Store.GetScheduleAsync(environmentId, Ct))!.PendingRequestId);
        Assert.Equal(ImageUpdateApplyOutcome.Discarded, await Store.ApplyPageAsync(environmentId, request, [], true, t1, Ct));
    }

    private async Task<Guid> RequestAsync(DateTimeOffset now)
    {
        var request = Guid.NewGuid();
        await Store.RequestNowAsync(environmentId, now, manual: false, Ct);
        Assert.True(await Store.TryAcquireAsync(environmentId, "replica-a", now, Ct));
        Assert.True(await Store.MarkRequestedAsync(environmentId, "replica-a", request, now, ImageUpdateLimits.NextDue(now, 0), Ct));
        return request;
    }

    private async Task<ImageUpdateCheck> GetAsync() => Assert.Single(await Store.ListAsync([environmentId], Ct), row => row.Reference == Reference);

    private static ImageUpdateResult Result(string state, string? reason, string? digest, string reference = Reference) =>
        new(reference, Platform, state, reason, digest, ImageId);

    /// <summary>O PostgreSQL guarda microssegundos.</summary>
    private static DateTimeOffset Trim(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, value.Offset);

    public async ValueTask DisposeAsync()
    {
        if (context is null) return;
        await context.EnvironmentRegistrations.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(CancellationToken.None);
        await context.DisposeAsync();
    }
}
