using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportFixture;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportResilienceTests;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T025 (D-003, SC-006): duas instâncias do remetente no mesmo banco entregam cada evento uma vez; se a dona do
/// lease morre, a outra assume depois de 30 s e reenvia o lote não confirmado com os mesmos <c>eventId</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportLeaseTests
{
    [Fact]
    public async Task TwoReplicas_DeliverEachEventOnce()
    {
        using var fixture = await CreateAsync();
        var transport = new StubTransport();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var a = fixture.Create(clock, null, transport);
        await using var b = fixture.Create(clock, null, transport);
        await CreateDestinationAsync(a, "siem-replicas");
        var events = new List<Guid>();
        for (var i = 0; i < 5; i++) events.Add(await AppendAsync(fixture));

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && !events.All(transport.Delivered().Contains))
        {
            await Task.WhenAll(a.Sender.RunOnceAsync("replica-a", Ct), b.Sender.RunOnceAsync("replica-b", Ct));
            await Task.Delay(200, Ct);
        }

        var delivered = transport.Delivered().Where(events.Contains).ToList();
        Assert.Equal(events, delivered);
        Assert.Single(transport.Sent.Select(item => item.Destination.DestinationId).Distinct());
    }

    [Fact]
    public async Task DeadOwner_IsReplacedAfterTheLease_AndTheBatchIsResentWithTheSameIds()
    {
        using var fixture = await CreateAsync();
        var start = DateTimeOffset.UtcNow;
        var dying = new StubTransport(AuditExportSendResult.Failed(AuditExportErrorCodes.Timeout));
        var survivor = new StubTransport();
        var clockA = new MutableClock(start);
        var clockB = new MutableClock(start);
        await using var a = fixture.Create(clockA, null, dying);
        await using var b = fixture.Create(clockB, null, survivor);
        var id = await CreateDestinationAsync(a, "siem-lease");
        var events = new List<Guid>();
        for (var i = 0; i < 3; i++) events.Add(await AppendAsync(fixture));

        // A réplica A assume o lease, envia e "morre" sem confirmar.
        await RunUntilAsync(a, clockA, () => dying.Sent.Any(item => events.All(item.Batch.Select(r => r.EventId).Contains)), "replica-a");
        var lease = (await a.Store.GetAsync(id, Ct))!.Cursor;
        Assert.Equal("replica-a", lease.LeaseOwner);

        // Antes de o lease vencer, B não envia nada.
        clockB.Now = lease.LeaseUntilUtc!.Value.AddSeconds(-1);
        await b.Sender.RunOnceAsync("replica-b", Ct);
        Assert.Empty(survivor.Sent);

        // Vencido o lease (30 s), B assume e reenvia os mesmos eventId.
        clockB.Now = lease.LeaseUntilUtc!.Value.AddSeconds(1);
        await RunUntilAsync(b, clockB, () => events.All(survivor.Delivered().Contains), "replica-b");
        Assert.Equal(events, survivor.Delivered().Where(events.Contains).ToList());
        Assert.Equal("replica-b", (await b.Store.GetAsync(id, Ct))!.Cursor.LeaseOwner);
        Assert.True(lease.LeaseUntilUtc!.Value - clockA.Now <= AuditExportLimits.LeaseDuration);
    }
}
