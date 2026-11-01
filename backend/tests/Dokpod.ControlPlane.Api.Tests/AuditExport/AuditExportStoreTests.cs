using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>Spec 027 T009 com PostgreSQL real: idempotência, limite de 5, nome único, versão, lease e exclusão durante o envio.</summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_IsIdempotent_LimitedToFive_AndUniqueByName()
    {
        await using var context = await CleanContextAsync();
        var store = new AuditExportStore(context);

        var first = await store.CreateAsync(Input("siem-1"), "key-1", Now, Ct);
        var replay = await store.CreateAsync(Input("ignorado"), "key-1", Now.AddHours(1), Ct);
        Assert.Equal(AuditExportWriteOutcome.Created, first.Outcome);
        Assert.Equal((AuditExportWriteOutcome.Replayed, first.Destination!.DestinationId), (replay.Outcome, replay.Destination!.DestinationId));
        Assert.Equal(AuditExportWriteOutcome.NameTaken, (await store.CreateAsync(Input("SIEM-1"), "key-x", Now, Ct)).Outcome);

        for (var i = 2; i <= 5; i++) Assert.Equal(AuditExportWriteOutcome.Created, (await store.CreateAsync(Input($"siem-{i}"), $"key-{i}", Now, Ct)).Outcome);
        Assert.Equal(AuditExportWriteOutcome.LimitReached, (await store.CreateAsync(Input("siem-6"), "key-6", Now, Ct)).Outcome);

        // Depois de 24 h a chave vale de novo; com o limite cheio, o novo pedido esbarra no limite, não na repetição.
        Assert.Equal(AuditExportWriteOutcome.LimitReached, (await store.CreateAsync(Input("siem-7"), "key-1", Now.AddHours(25), Ct)).Outcome);

        var state = (await store.GetAsync(first.Destination.DestinationId, Ct))!;
        Assert.Equal(["audit_export", "containers"], state.Destination.Categories);
        Assert.True(state.Cursor.Position.TxId > 0);
    }

    [Fact]
    public async Task UpdateAndDelete_UseTheVersion()
    {
        await using var context = await CleanContextAsync();
        var store = new AuditExportStore(context);
        var created = (await store.CreateAsync(Input("siem-a"), "key-a", Now, Ct)).Destination!;
        await store.CreateAsync(Input("siem-b"), "key-b", Now, Ct);

        Assert.Equal(AuditExportWriteOutcome.VersionConflict, (await store.UpdateAsync(created.DestinationId, Input("siem-a2"), 99, Now, Ct)).Outcome);
        Assert.Equal(AuditExportWriteOutcome.NameTaken, (await store.UpdateAsync(created.DestinationId, Input("siem-b"), 1, Now, Ct)).Outcome);
        var updated = await store.UpdateAsync(created.DestinationId, Input("siem-a2") with { Active = false }, 1, Now.AddMinutes(1), Ct);
        Assert.Equal((AuditExportWriteOutcome.Updated, 2L, false), (updated.Outcome, updated.Destination!.Version, updated.Destination.Active));

        Assert.Equal(AuditExportWriteOutcome.VersionConflict, await store.DeleteAsync(created.DestinationId, 1, Ct));
        Assert.Equal(AuditExportWriteOutcome.Deleted, await store.DeleteAsync(created.DestinationId, 2, Ct));
        Assert.Equal(AuditExportWriteOutcome.NotFound, await store.DeleteAsync(created.DestinationId, 2, Ct));
        Assert.Equal(AuditExportWriteOutcome.NotFound, (await store.UpdateAsync(created.DestinationId, Input("x"), 2, Now, Ct)).Outcome);
    }

    [Fact]
    public async Task Lease_IsExclusive_ExpiresAndOnlyTheOwnerAdvancesForward()
    {
        await using var context = await CleanContextAsync();
        var store = new AuditExportStore(context);
        var id = (await store.CreateAsync(Input("siem-l"), "key-l", Now, Ct)).Destination!.DestinationId;
        var start = (await store.GetAsync(id, Ct))!.Cursor.Position;
        var next = new AuditExportPosition(start.TxId + 10, 5);

        Assert.True(await store.TryAcquireLeaseAsync(id, "replica-a", Now, AuditExportLimits.LeaseDuration, Ct));
        Assert.False(await store.TryAcquireLeaseAsync(id, "replica-b", Now.AddSeconds(10), AuditExportLimits.LeaseDuration, Ct));
        Assert.False(await store.AdvanceAsync(id, "replica-b", next, Now, Ct));
        Assert.True(await store.AdvanceAsync(id, "replica-a", next, Now.AddSeconds(1), Ct));
        Assert.False(await store.AdvanceAsync(id, "replica-a", start, Now.AddSeconds(2), Ct));
        Assert.True(await store.RecordFailureAsync(id, "replica-a", AuditExportErrorCodes.Timeout, Now.AddSeconds(3), Ct));

        Assert.True(await store.TryAcquireLeaseAsync(id, "replica-b", Now.AddSeconds(31), AuditExportLimits.LeaseDuration, Ct));
        Assert.False(await store.RecordFailureAsync(id, "replica-a", AuditExportErrorCodes.Timeout, Now.AddSeconds(32), Ct));
        var cursor = (await store.GetAsync(id, Ct))!.Cursor;
        Assert.Equal((next, "replica-b", AuditExportErrorCodes.Timeout), (cursor.Position, cursor.LeaseOwner, cursor.LastErrorCode));

        await store.ReleaseLeaseAsync(id, "replica-b", Ct);
        Assert.Null((await store.GetAsync(id, Ct))!.Cursor.LeaseOwner);
    }

    [Fact]
    public async Task DeletingDuringSend_MakesTheSenderLoseItsLease()
    {
        await using var context = await CleanContextAsync();
        var store = new AuditExportStore(context);
        var id = (await store.CreateAsync(Input("siem-d"), "key-d", Now, Ct)).Destination!.DestinationId;
        Assert.True(await store.TryAcquireLeaseAsync(id, "replica-a", Now, AuditExportLimits.LeaseDuration, Ct));

        Assert.Equal(AuditExportWriteOutcome.Deleted, await store.DeleteAsync(id, 1, Ct));

        Assert.False(await store.TryAcquireLeaseAsync(id, "replica-a", Now.AddSeconds(5), AuditExportLimits.LeaseDuration, Ct));
        Assert.False(await store.AdvanceAsync(id, "replica-a", new AuditExportPosition(ulong.MaxValue, 1), Now, Ct));
    }

    private static AuditExportDestinationInput Input(string name) =>
        new AuditExportDestinationInput(name, "syslog", "siem.exemplo.interno", 6514, null, "siem-soc", ["containers"], true).Validate().Normalized!;

    private static async Task<Dokpod.ControlPlane.Infrastructure.ControlPlaneDbContext> CleanContextAsync()
    {
        var connectionString = ConnectionStringOrSkip();
        await using (var connection = await OpenAsync(connectionString, Ct))
        {
            await ExecuteAsync(connection, null, "DELETE FROM dokpod.audit_export_destinations", Ct);
        }

        return CreateContext(connectionString);
    }
}
