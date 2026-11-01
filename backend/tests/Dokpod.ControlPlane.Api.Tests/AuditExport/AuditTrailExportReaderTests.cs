using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportMigrationTests;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T008 (D-001) com PostgreSQL real: transações que confirmam fora da ordem de <c>occurred_at_utc</c> não
/// perdem eventos, e uma transação aberta segura o cursor em vez de deixá-lo pular.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditTrailExportReaderTests
{
    private const string Action = "container.restart";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OutOfOrderCommits_AreAllRead_InCommitOrder_AndAnOpenTransactionHoldsTheCursor()
    {
        var connectionString = ConnectionStringOrSkip();
        await using var context = CreateContext(connectionString);
        var reader = new AuditTrailExportReader(context);
        var start = await reader.HorizonAsync(Ct);

        await using var slow = await OpenAsync(connectionString, Ct);
        await using var slowTransaction = await slow.BeginTransactionAsync(Ct);
        var early = await AppendAuditEventAsync(slow, slowTransaction, Action, DateTimeOffset.UtcNow.AddSeconds(-5));

        await using var fast = await OpenAsync(connectionString, Ct);
        var late = await AppendAuditEventAsync(fast, null, Action, DateTimeOffset.UtcNow);

        // A transação lenta ainda está aberta: o evento rápido já confirmou, mas fica atrás do xmin e não é lido.
        Assert.DoesNotContain(await ReadAllAsync(reader, start), item => item.Event.EventId == late);

        await slowTransaction.CommitAsync(Ct);
        var items = await WaitForAsync(reader, start, [early, late]);

        var ids = items.Select(item => item.Event.EventId).ToList();
        Assert.True(ids.IndexOf(early) < ids.IndexOf(late));
        Assert.Equal(items.Select(item => item.Position).Order().ToList(), items.Select(item => item.Position).ToList());

        var pending = await reader.PendingAsync(start, [Action], 1, Ct);
        Assert.True(pending.Capped);
        Assert.Equal(1, pending.Count);
        Assert.NotNull(pending.OldestAtUtc);
    }

    [Fact]
    public async Task OnlyRequestedActions_AreRead_AndTheHorizonSkipsThePast()
    {
        var connectionString = ConnectionStringOrSkip();
        await using var connection = await OpenAsync(connectionString, Ct);
        var before = await AppendAuditEventAsync(connection, null, Action);
        await using var context = CreateContext(connectionString);
        var reader = new AuditTrailExportReader(context);
        var horizon = await WaitForHorizonAfterAsync(reader, before);

        var other = await AppendAuditEventAsync(connection, null, "container.stop");
        var wanted = await AppendAuditEventAsync(connection, null, Action);
        var items = await WaitForAsync(reader, horizon, [wanted]);

        Assert.DoesNotContain(items, item => item.Event.EventId == before);
        Assert.DoesNotContain(items, item => item.Event.EventId == other);
        Assert.Empty(await reader.ReadAsync(horizon, [], 10, Ct));
    }

    private static async Task<List<AuditTrailExportItem>> ReadAllAsync(AuditTrailExportReader reader, AuditExportPosition after)
    {
        var all = new List<AuditTrailExportItem>();
        while (true)
        {
            var batch = await reader.ReadAsync(after, [Action], 500, Ct);
            if (batch.Count == 0) return all;
            all.AddRange(batch);
            after = batch[^1].Position;
        }
    }

    /// <summary>Outras classes de teste no mesmo banco podem segurar o xmin por instantes; espera até 15 s.</summary>
    private static async Task<List<AuditTrailExportItem>> WaitForAsync(AuditTrailExportReader reader, AuditExportPosition after, Guid[] expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            var items = await ReadAllAsync(reader, after);
            if (expected.All(id => items.Any(item => item.Event.EventId == id)) || DateTimeOffset.UtcNow > deadline)
            {
                Assert.All(expected, id => Assert.Contains(items, item => item.Event.EventId == id));
                return items;
            }

            await Task.Delay(200, Ct);
        }
    }

    private static async Task<AuditExportPosition> WaitForHorizonAfterAsync(AuditTrailExportReader reader, Guid committed)
    {
        // O horizonte só passa do evento já confirmado quando nenhuma transação mais antiga está aberta.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            var horizon = await reader.HorizonAsync(Ct);
            var visibleAfter = await ReadAllAsync(reader, horizon);
            if (visibleAfter.All(item => item.Event.EventId != committed) || DateTimeOffset.UtcNow > deadline) return horizon;
            await Task.Delay(200, Ct);
        }
    }
}
