using System.Diagnostics.Metrics;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportFixture;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T024 com PostgreSQL real e relógio injetável: destino fora (cursor parado, <c>failing</c>, fila contada até
/// 10.000), volta sem perda nem duplicação na ordem de <c>(tx_id, export_seq)</c>, lacuna além de 72 h, espera
/// exponencial, um destino fora não segura o outro e transação longa segura o cursor sem perder eventos (D-001).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportResilienceTests
{
    private const string Owner = "replica-t024";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RejectedDestination_HoldsTheCursor_ThenDeliversEverythingInOrderOnce()
    {
        using var fixture = await CreateAsync();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var transport = new StubTransport(AuditExportSendResult.Failed(AuditExportErrorCodes.Rejected));
        await using var services = fixture.Create(clock, null, transport);
        var id = await CreateDestinationAsync(services, "siem-volta");
        var start = (await services.Store.GetAsync(id, Ct))!.Cursor.Position;

        var events = new List<Guid>();
        for (var i = 0; i < 3; i++) events.Add(await AppendAsync(fixture));
        await RunUntilAsync(services, clock, () => transport.Sent.Any(item => item.Batch.Select(r => r.EventId).Intersect(events).Count() == 3));

        var failing = (await services.Store.GetAsync(id, Ct))!;
        Assert.Equal(start, failing.Cursor.Position);
        Assert.Equal(AuditExportErrorCodes.Rejected, failing.Cursor.LastErrorCode);
        var listed = Assert.Single(await services.Service.ListAsync(Ct));
        // Os 3 eventos e o próprio audit_export.destination_created (categoria obrigatória, L7).
        Assert.Equal((AuditExportStates.Failing, 4L), (listed.State, listed.Pending.Count));
        Assert.Empty(transport.Delivered());

        // Na espera exponencial, o mesmo instante não tenta de novo.
        var attempts = transport.Sent.Count;
        await services.Sender.RunOnceAsync(Owner, Ct);
        Assert.Equal(attempts, transport.Sent.Count);

        transport.Respond = _ => AuditExportSendResult.Ok;
        await RunUntilAsync(services, clock, () => events.All(transport.Delivered().Contains));

        var delivered = transport.Delivered().Where(events.Contains).ToList();
        Assert.Equal(events, delivered);
        var recovered = Assert.Single(await services.Service.ListAsync(Ct));
        Assert.Equal((AuditExportStates.Ok, (string?)null), (recovered.State, recovered.Cursor.LastErrorCode));
        Assert.NotNull(recovered.Cursor.LastSentAtUtc);
    }

    [Fact]
    public async Task Pending_IsCountedUpTo10000_AndMarkedCapped()
    {
        using var fixture = await CreateAsync();
        var transport = new StubTransport(AuditExportSendResult.Failed(AuditExportErrorCodes.ConnectionRefused));
        await using var services = fixture.Create(new MutableClock(DateTimeOffset.UtcNow), null, transport);
        await CreateDestinationAsync(services, "siem-cheia", "containers");

        await using (var connection = await OpenAsync(fixture.ConnectionString, Ct))
        {
            await ExecuteAsync(
                connection,
                null,
                """
                SELECT dokpod.dokpod_append_audit_event(gen_random_uuid(), now(), gen_random_uuid(), 1::smallint, 'tester', 'container.restart',
                    gen_random_uuid(), NULL, 0::smallint, NULL, sha256(convert_to(g::text || clock_timestamp()::text, 'UTF8')), NULL, NULL)
                FROM generate_series(1, 10001) AS g
                """,
                Ct);
        }

        var listed = Assert.Single(await services.Service.ListAsync(Ct));
        Assert.Equal((AuditExportLimits.MaxPendingCount, true), (listed.Pending.Count, listed.Pending.Capped));
        Assert.NotNull(listed.Pending.OldestAtUtc);
    }

    [Fact]
    public async Task PendingOlderThan72Hours_IsSkippedAsAGap_AndAudited()
    {
        using var fixture = await CreateAsync();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var transport = new StubTransport();
        await using var services = fixture.Create(clock, null, transport);
        var id = await CreateDestinationAsync(services, "siem-lacuna");
        // D-002 pula a sequência inicial de pendentes fora da janela: primeiro sai o evento recente da criação.
        await RunUntilAsync(services, clock, () => transport.Batches.SelectMany(batch => batch).Any(record => record.Action == "audit_export.destination_created"));
        var oldAt = clock.Now.AddHours(-73);
        var olderAt = clock.Now.AddHours(-80);
        var old = await AppendAsync(fixture, at: oldAt);
        var older = await AppendAsync(fixture, at: olderAt);
        var recent = await AppendAsync(fixture);

        await RunUntilAsync(services, clock, () => transport.Delivered().Contains(recent));

        Assert.DoesNotContain(old, transport.Delivered());
        Assert.DoesNotContain(older, transport.Delivered());
        var gapRecord = Assert.Single(transport.Batches.SelectMany(batch => batch), record => record.Action == "audit_export.gap" && record.SubjectId == id.ToString("D"));
        Assert.Equal(("failed", "retention_window_exceeded", "2"), (gapRecord.Outcome, gapRecord.FailureCode, gapRecord.Details["count"]));
        Assert.Equal(olderAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss"), gapRecord.Details["fromUtc"][..19]);
        Assert.Equal(oldAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss"), gapRecord.Details["toUtc"][..19]);
        Assert.Equal(Guid.Empty, gapRecord.EnvironmentId);

        var listed = Assert.Single(await services.Service.ListAsync(Ct));
        Assert.Equal(AuditExportStates.Gap, listed.State);
        Assert.NotNull(listed.Cursor.LastGapAtUtc);
    }

    [Fact]
    public void Backoff_DoublesUpTo60Seconds()
    {
        Assert.Equal(
            [1, 2, 4, 8, 16, 32, 60, 60],
            Enumerable.Range(1, 8).Select(failures => AuditExportBackoff.Delay(failures).TotalSeconds));
        var backoff = new AuditExportBackoff();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        backoff.Fail(id, now);
        backoff.Fail(id, now);
        Assert.True(backoff.IsWaiting(id, now.AddSeconds(1.5)));
        Assert.False(backoff.IsWaiting(id, now.AddSeconds(2)));
        backoff.Reset(id);
        Assert.False(backoff.IsWaiting(id, now));
    }

    [Fact]
    public async Task OneDestinationDown_DoesNotHoldTheOther()
    {
        using var fixture = await CreateAsync();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var transport = new StubTransport { Respond = destination => destination.Name == "siem-fora" ? AuditExportSendResult.Failed(AuditExportErrorCodes.Timeout) : AuditExportSendResult.Ok };
        await using var services = fixture.Create(clock, null, transport);
        await CreateDestinationAsync(services, "siem-fora");
        await CreateDestinationAsync(services, "siem-ativo");
        var restart = await AppendAsync(fixture);

        await RunUntilAsync(services, clock, () => transport.Delivered("siem-ativo").Contains(restart) && transport.Sent.Any(item => item.Destination.Name == "siem-fora"));

        var states = (await services.Service.ListAsync(Ct)).ToDictionary(view => view.Destination.Name, view => view.State);
        Assert.Equal(AuditExportStates.Failing, states["siem-fora"]);
        Assert.Equal(AuditExportStates.Ok, states["siem-ativo"]);
        Assert.Empty(transport.Delivered("siem-fora"));
    }

    [Fact]
    public async Task LongTransaction_HoldsTheCursorWithoutLosingEvents_AndShowsTheLag()
    {
        using var fixture = await CreateAsync();
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var transport = new StubTransport();
        await using var services = fixture.Create(clock, null, transport);
        var id = await CreateDestinationAsync(services, "siem-longa");

        await using var slow = await OpenAsync(fixture.ConnectionString, Ct);
        await using var transaction = await slow.BeginTransactionAsync(Ct);
        var first = await AuditExportMigrationTests.AppendAuditEventAsync(slow, transaction, "network.remove");
        var second = await AppendAsync(fixture, at: clock.Now.AddMinutes(-5));

        for (var i = 0; i < 3; i++)
        {
            clock.Now = clock.Now.AddSeconds(61);
            await services.Sender.RunOnceAsync(Owner, Ct);
        }

        Assert.DoesNotContain(second, transport.Delivered());
        var listed = Assert.Single(await services.Service.ListAsync(Ct));
        Assert.True(listed.Pending.Count >= 1);
        Assert.NotNull(listed.Pending.OldestAtUtc);
        Assert.True(Lag(fixture.Metrics, id) >= 290, "o atraso do pendente mais antigo deveria aparecer na métrica");

        await transaction.CommitAsync(Ct);
        await RunUntilAsync(services, clock, () => transport.Delivered().Contains(second));

        var delivered = transport.Delivered().Where(eventId => eventId == first || eventId == second).ToList();
        Assert.Equal([first, second], delivered);
    }

    /// <summary>Por padrão só a categoria de redes, que os outros testes quase não gravam: contagens e ordem ficam exatas.</summary>
    internal static async Task<Guid> CreateDestinationAsync(AuditExportFixture.Services services, string name, string category = "networks")
    {
        var result = await services.Service.CreateAsync(
            new AuditExportDestinationInput(name, "syslog", "10.20.30.40", 6514, null, null, [category], true),
            Guid.NewGuid().ToString(),
            new AuditExportCaller("user-027", null, Guid.NewGuid()),
            Ct);
        Assert.Equal(AuditExportServiceOutcome.Created, result.Outcome);
        return result.View!.Destination.DestinationId;
    }

    internal static async Task<Guid> AppendAsync(AuditExportFixture fixture, string action = "network.remove", DateTimeOffset? at = null)
    {
        await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
        return await AuditExportMigrationTests.AppendAuditEventAsync(connection, null, action, at);
    }

    /// <summary>Roda ciclos avançando o relógio além da espera máxima; transações de outros testes podem segurar o xmin.</summary>
    internal static async Task RunUntilAsync(AuditExportFixture.Services services, MutableClock clock, Func<bool> condition, string owner = Owner)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline && !condition())
        {
            clock.Now = clock.Now.AddSeconds(61);
            await services.Sender.RunOnceAsync(owner, Ct);
            if (!condition()) await Task.Delay(200, Ct);
        }

        Assert.True(condition(), "a condição de entrega não foi atingida a tempo");
    }

    private static double Lag(AuditExportMetrics metrics, Guid destinationId)
    {
        double? lag = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter && instrument.Name == "dokpod.audit_export.lag_seconds") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "destination.id" && (string?)tag.Value == destinationId.ToString("D")) lag = value;
            }
        });
        listener.Start();
        listener.RecordObservableInstruments();
        return lag ?? -1;
    }
}
