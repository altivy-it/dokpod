using System.Diagnostics;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportFixture;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T035 (SC-004): ação em lote de 100 containers (100 comandos auditados, cada um com seu evento na trilha)
/// medida 30 vezes com a exportação ligada e 30 desligada. Ligada, um destino ativo e o ciclo do remetente rodam no
/// ritmo do worker (<see cref="AuditExportLimits.BatchWait"/>) em outra conexão. As rodadas se alternam para que a
/// deriva do banco caia nos dois lados. Só com a exportação desligada, o p95 de 30 amostras já varia uns 8% entre
/// execuções; por isso a comparação de 30 + 30 roda três vezes e vale a mediana das três razões.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresPerformanceCollection.Name)]
public sealed class AuditExportBatchImpactTests
{
    private const int Containers = 100;
    private const int Runs = 30;
    private const int WarmUp = 5;
    private const int Trials = 3;

    [Fact]
    public async Task BatchOf100_WithExportOn_StaysWithin5PercentOfTheP95WithExportOff()
    {
        using var fixture = await CreateAsync();
        TestPki.WriteSecret(fixture.SecretsRoot, "siem-soc", null);
        await using var services = fixture.Create(null, new StubTransport());
        await using var senderServices = fixture.Create(null, new StubTransport());
        var context = services.Context;
        var environmentId = Guid.NewGuid();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t035-{environmentId:N}"[..20], "lab-host", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.Manage]), Ct);
        var store = new PostgresAgentCommandStore(context);
        var destination = await CreateDestinationAsync(services);
        var ratios = new List<double>();
        try
        {
            for (var trial = 1; trial <= Trials; trial++)
            {
                var off = new List<double>();
                var on = new List<double>();
                for (var round = 0; round < WarmUp + Runs; round++)
                {
                    // Ordem alternada: rodadas pares começam desligadas, ímpares ligadas.
                    foreach (var exporting in round % 2 == 0 ? new[] { false, true } : [true, false])
                    {
                        var elapsed = await MeasureAsync(store, senderServices, destination, environmentId, exporting);
                        if (round >= WarmUp) (exporting ? on : off).Add(elapsed);
                    }
                }

                var (p95Off, p95On) = (P95(off), P95(on));
                ratios.Add(p95On / p95Off);
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"SC-004 lote de {Containers}, comparação {trial}: p95 desligada={p95Off:0.0} ms, ligada={p95On:0.0} ms ({p95On / p95Off:P1}); medianas {P50(off):0.0} / {P50(on):0.0} ms");
            }
        }
        finally
        {
            await context.AgentCommands.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(Ct);
            await context.EnvironmentRegistrations.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(Ct);
        }

        var ratio = P50(ratios);
        TestContext.Current.TestOutputHelper?.WriteLine($"SC-004 mediana das razões p95 ligada/desligada: {ratio:P1}");
        Assert.True(ratio <= 1.05, $"p95 ligada ficou em {ratio:P1} da desligada (mediana de {Trials} comparações), acima de 105%");
    }

    private static async Task<double> MeasureAsync(
        PostgresAgentCommandStore store, AuditExportFixture.Services senderServices, Guid destination, Guid environmentId, bool exporting)
    {
        await SetActiveAsync(senderServices.Context, destination, exporting);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var sender = exporting ? SendLoopAsync(senderServices.Sender, stop.Token) : Task.CompletedTask;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < Containers; i++)
        {
            var now = DateTimeOffset.UtcNow;
            var commandId = Guid.NewGuid();
            var command = new PersistedAgentCommand(
                new AgentCommand(environmentId, commandId, AgentCommandKind.RestartContainer, $"{i:x64}", "revision-01", new string('A', 64), now.AddMinutes(1), 1),
                ControlPlaneCommandState.Pending,
                now,
                now);
            var audit = AuditEvent.Create(Guid.NewGuid(), now, Guid.NewGuid(), AuditActorKind.User, "user-t035", "container.restart", environmentId, AuditOutcome.Succeeded, commandId: commandId);
            Assert.Equal(AgentCommandEnqueueResult.Created, await store.EnqueueAuditedAsync(command, audit, Ct));
        }

        var elapsed = watch.Elapsed.TotalMilliseconds;
        await stop.CancelAsync();
        await sender;
        return elapsed;
    }

    /// <summary>Mesmo ritmo do <c>AuditExportWorker</c>: um ciclo, e outro logo em seguida enquanto o lote vier cheio.</summary>
    private static async Task SendLoopAsync(AuditExportSender sender, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (await sender.RunOnceAsync("replica-t035", cancellationToken) >= AuditExportLimits.MaxBatch)
                {
                }

                await Task.Delay(AuditExportLimits.BatchWait, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<Guid> CreateDestinationAsync(AuditExportFixture.Services services)
    {
        var result = await services.Service.CreateAsync(
            new AuditExportDestinationInput("siem-t035", AuditExportProtocols.Syslog, "10.20.30.40", 6514, null, "siem-soc", ["containers"], true),
            Guid.NewGuid().ToString(),
            new AuditExportCaller("user-027", "Maria", Guid.NewGuid()),
            Ct);
        Assert.Equal(AuditExportServiceOutcome.Created, result.Outcome);
        return result.View!.Destination.DestinationId;
    }

    private static Task SetActiveAsync(ControlPlaneDbContext context, Guid destination, bool active) =>
        context.Database.ExecuteSqlInterpolatedAsync($"UPDATE dokpod.audit_export_destinations SET active = {active} WHERE destination_id = {destination}", Ct);

    private static double P95(List<double> values) => Percentile(values, 0.95);

    private static double P50(List<double> values) => Percentile(values, 0.50);

    private static double Percentile(List<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }
}
