using System.Diagnostics;
using Dokpod.ControlPlane.Api.Agents;
using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ChangeWindows;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T035 (D-011): uma ação de container sobre PostgreSQL real, 30 vezes com a janela configurada (aberta, para
/// o comando ser aceito: o guard lê <c>change_windows</c> e procura o <c>commandId</c>) e 30 sem janela (calendário vazio
/// da CE). As rodadas se alternam para a deriva do banco cair nos dois lados, e a comparação roda cinco vezes; vale a
/// mediana das diferenças de p95, que não pode passar de 5 ms. Como a 027 (SC-004), a cauda varia com CPU disputada,
/// então a medição só roda quando pedida, isolada: <c>DOKPOD_TEST_026_IMPACT=1</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresPerformanceCollection.Name)]
public sealed class ChangeWindowGuardImpactTests
{
    private const int Runs = 30;
    private const int WarmUp = 5;
    private const int Trials = 5;
    private const double MaxDifferenceMs = 5;
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ContainerAction_WithAWindow_AddsAtMost5MsToTheP95()
    {
        if (Environment.GetEnvironmentVariable("DOKPOD_TEST_026_IMPACT") != "1") Assert.Skip("Medição da T035 só isolada (DOKPOD_TEST_026_IMPACT=1).");
        var connectionString = ConnectionStringOrSkip();
        await using var context = CreateContext(connectionString);
        var environmentId = Guid.NewGuid();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t035-{environmentId:N}"[..20], "lab-host", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Development), Ct);
        var windows = new ChangeWindowStore(context);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.change_windows WHERE stage = 'development'", Ct);
        Assert.Equal(ChangeWindowWriteOutcome.Saved, (await windows.SaveAsync(
            new ChangeWindow(
                EnvironmentStage.Development,
                "America/Sao_Paulo",
                [new ChangeWindowRange(Enum.GetValues<DayOfWeek>().ToHashSet(), new TimeOnly(0, 0), new TimeOnly(23, 59))],
                0,
                "t035",
                Now),
            null,
            Ct)).Outcome);
        var withWindow = Service(context, windows, environmentId);
        var withoutWindow = Service(context, new NoChangeWindows(), environmentId);
        var differences = new List<double>();
        try
        {
            for (var trial = 1; trial <= Trials; trial++)
            {
                var off = new List<double>();
                var on = new List<double>();
                for (var round = 0; round < WarmUp + Runs; round++)
                {
                    foreach (var windowed in round % 2 == 0 ? new[] { false, true } : [true, false])
                    {
                        var elapsed = await MeasureAsync(windowed ? withWindow : withoutWindow, environmentId);
                        if (round >= WarmUp) (windowed ? on : off).Add(elapsed);
                    }
                }

                differences.Add(P95(on) - P95(off));
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"T035 comparação {trial}: p95 sem janela={P95(off):0.00} ms, com janela={P95(on):0.00} ms; medianas {P50(off):0.00} / {P50(on):0.00} ms");
            }
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.change_windows WHERE stage = 'development'", Ct);
            await context.AgentCommands.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(Ct);
            await context.EnvironmentRegistrations.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(Ct);
        }

        var difference = P50(differences);
        TestContext.Current.TestOutputHelper?.WriteLine($"T035 mediana das diferenças de p95: {difference:0.00} ms");
        Assert.True(difference <= MaxDifferenceMs, $"A janela somou {difference:0.00} ms ao p95 (máximo {MaxDifferenceMs} ms).");
    }

    private static async Task<double> MeasureAsync((ContainerCommandService Service, InMemoryAgentSessionStore Sessions) target, Guid environmentId)
    {
        var watch = Stopwatch.StartNew();
        var result = await target.Service.SubmitAsync(
            environmentId, Guid.NewGuid(), AgentCommandKind.RestartContainer, new string('c', 64), "rev-1", Now.AddMinutes(10),
            AuthenticatedActor.FromSubject("t035"), "token", Guid.NewGuid(), Ct);
        watch.Stop();
        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, result.Outcome);
        return watch.Elapsed.TotalMilliseconds;
    }

    private static (ContainerCommandService, InMemoryAgentSessionStore) Service(ControlPlaneDbContext context, IChangeWindowSchedule schedule, Guid environmentId)
    {
        var clock = new FixedTime(Now);
        var decider = new AllowAll();
        var commands = new PostgresAgentCommandStore(context);
        var audit = new PostgresAuditEventWriter(context);
        var sessions = new InMemoryAgentSessionStore();
        sessions.ActivateAsync(environmentId, Ct).AsTask().GetAwaiter().GetResult();
        var service = new ContainerCommandService(
            decider,
            new PostgresEnvironmentRegistrationStore(context),
            sessions,
            new AgentCommandQueueService(commands, new InMemoryAgentCommandDeliveryQueue(), clock),
            new ChangeWindowGuard(schedule, commands, decider, audit, clock),
            new PostgresActionJustificationStore(context),
            new PostgresContainerSearchStore(context),
            clock);
        return (service, sessions);
    }

    private static double P95(List<double> samples) => Percentile(samples, 0.95);

    private static double P50(List<double> samples) => Percentile(samples, 0.50);

    private static double Percentile(List<double> samples, double percentile)
    {
        var ordered = samples.Order().ToArray();
        return ordered[(int)Math.Ceiling(percentile * ordered.Length) - 1];
    }

    private sealed class AllowAll : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
