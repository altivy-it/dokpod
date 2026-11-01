using System.Net;
using System.Text;
using System.Threading.Channels;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.ControlPlane.Application.Terminal;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Google.Protobuf;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportFixture;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T034 (SC-003): uma sessão de terminal real grava na trilha PostgreSQL com um segredo na justificativa e
/// conteúdo digitado no terminal. Os dois transportes entregam os eventos <c>container.exec.*</c> sem nenhum dos dois
/// valores: a exportação só repete o que a trilha guardou, e a trilha guarda só metadados.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportCanaryTests : IAsyncLifetime
{
    private const string Owner = "replica-t034";
    private const string SyslogHost = "siem.test";
    private const string OtlpHost = "otel.test";
    private static readonly Guid Env = Guid.NewGuid();
    private static readonly string ContainerId = new('b', 64);

    // Montados por concatenação para não parecerem credenciais literais na varredura de segredos.
    private static readonly string TerminalCanary = "canario-027-" + "terminal";
    private static readonly string SecretCanary = "canario027" + "segredo";

    private readonly TestPki pki = new();
    private AuditExportFixture? fixture;
    private System.Security.Cryptography.X509Certificates.X509Certificate2? syslogCertificate;
    private System.Security.Cryptography.X509Certificates.X509Certificate2? otlpCertificate;
    private SyslogTlsTestServer? syslog;
    private OtlpTestReceiver? otlp;

    private AuditExportFixture Fixture => fixture!;

    public async ValueTask InitializeAsync()
    {
        fixture = await CreateAsync("\"audit-siem-export\"");
        TestPki.WriteSecret(Fixture.SecretsRoot, "siem-soc", pki.CaPem);
        syslogCertificate = pki.Issue(SyslogHost, server: true);
        otlpCertificate = pki.Issue(OtlpHost, server: true);
        syslog = SyslogTlsTestServer.Tls(syslogCertificate);
        otlp = await OtlpTestReceiver.StartAsync(otlpCertificate, Ct);
    }

    [Fact]
    public async Task SecretAndTerminalContent_NeverReachEitherTransport()
    {
        var resolver = new FixedResolver(IPAddress.Loopback);
        var secrets = new MountedSecretReader(Fixture.SecretsRoot);
        await using var services = Fixture.Create(resolver, new SyslogTlsTransport(resolver, secrets), new OtlpHttpTransport(resolver, secrets));
        await CreateDestinationAsync(services, "siem-t034", AuditExportProtocols.Syslog, SyslogHost, syslog!.Port, null);
        await CreateDestinationAsync(services, "otel-t034", AuditExportProtocols.Otlp, OtlpHost, otlp!.Port, AuditExportProtocols.DefaultOtlpPath);

        var echoed = await RunTerminalAsync(services.Context, "Investigar incidente INC-27 " + "tok" + "en=" + SecretCanary);
        Assert.Contains(TerminalCanary, echoed);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline && (ExecActions(syslog.Records.Select(r => r["action"]!.ToString())) < 3 || ExecActions(OtlpActions()) < 3))
        {
            await services.Sender.RunOnceAsync(Owner, Ct);
            await Task.Delay(200, Ct);
        }

        Assert.Equal(3, ExecActions(syslog.Records.Select(r => r["action"]!.ToString())));
        Assert.Equal(3, ExecActions(OtlpActions()));
        var syslogText = string.Concat(syslog.Messages);
        var otlpText = string.Concat(otlp.Received.Select(item => item.Request.ToString() + Encoding.UTF8.GetString(item.Request.ToByteArray())));
        foreach (var canary in new[] { TerminalCanary, SecretCanary })
        {
            Assert.DoesNotContain(canary, syslogText);
            Assert.DoesNotContain(canary, otlpText);
        }

        // A justificativa chega mascarada, sinal de que os dois viram o mesmo campo que a trilha guardou.
        Assert.Contains("token=***", syslogText);
        Assert.Contains("token=***", otlpText);
    }

    private IEnumerable<string> OtlpActions() =>
        otlp!.Received.SelectMany(item => item.Request.ResourceLogs).SelectMany(r => r.ScopeLogs).SelectMany(s => s.LogRecords).Select(l => l.Body.StringValue);

    private static int ExecActions(IEnumerable<string> actions) => actions.Count(action => action.StartsWith("container.exec.", StringComparison.Ordinal));

    private static async Task CreateDestinationAsync(AuditExportFixture.Services services, string name, string protocol, string host, int port, string? path)
    {
        var result = await services.Service.CreateAsync(
            new AuditExportDestinationInput(name, protocol, host, port, path, "siem-soc", ["terminal"], true),
            Guid.NewGuid().ToString(),
            new AuditExportCaller("user-027", "Maria", Guid.NewGuid()),
            Ct);
        Assert.Equal(AuditExportServiceOutcome.Created, result.Outcome);
    }

    /// <summary>Sessão real do <see cref="TerminalSessionService"/> com agente simulado que ecoa a entrada.</summary>
    private static async Task<string> RunTerminalAsync(ControlPlaneDbContext context, string justification)
    {
        var options = new TerminalOptions();
        var registry = new TerminalSessionRegistry(options);
        var dispatcher = new AgentQueryDispatcher(TimeProvider.System);
        var inbox = Channel.CreateUnbounded<ControlPlaneMessage>();
        dispatcher.Register(Env, Guid.NewGuid(), [Dokpod.Agent.Contracts.V1.Capability.ContainerExec], inbox.Writer);
        var service = new TerminalSessionService(
            new AllowAll(), new ProductionRegistration(), new RunningContainer(), dispatcher, registry, new PostgresAuditEventWriter(context), options, TimeProvider.System);

        var agent = EchoAgentAsync(inbox.Reader, registry);
        var output = Channel.CreateUnbounded<TerminalOutputFrame>();
        await service.RunAsync(
            new TerminalOpenRequest(Env, ContainerId, 100, 30, justification),
            AuthenticatedActor.FromSubject("u-027"),
            "token",
            DateTimeOffset.UtcNow.AddHours(1),
            null,
            true,
            Guid.NewGuid(),
            Input(new TerminalInputFrame(Encoding.UTF8.GetBytes("echo " + TerminalCanary + "\n"), null, null)),
            output.Writer,
            Ct);
        output.Writer.TryComplete();
        await agent;
        var frames = await output.Reader.ReadAllAsync(Ct).ToListAsync(Ct);
        return string.Concat(frames.Where(frame => frame.Data is not null).Select(frame => Encoding.UTF8.GetString(frame.Data!)));
    }

    private static async Task EchoAgentAsync(ChannelReader<ControlPlaneMessage> inbox, TerminalSessionRegistry registry)
    {
        var open = (await inbox.ReadAsync(Ct)).ExecOpen;
        var session = registry.Attach(Guid.Parse(open.ExecSessionId), Env)!;
        await session.FromAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Opened, Shell = "/bin/sh" } }, Ct);
        await foreach (var frame in session.ToAgent.Reader.ReadAllAsync(Ct))
        {
            if (frame.FrameCase == ExecFrame.FrameOneofCase.Stdin)
            {
                await session.FromAgent.Writer.WriteAsync(new ExecFrame { Output = frame.Stdin }, Ct);
            }
            else if (frame.FrameCase == ExecFrame.FrameOneofCase.Status)
            {
                await session.FromAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = frame.Status.Reason } }, Ct);
                break;
            }
        }

        session.FromAgent.Writer.TryComplete();
    }

    private static async IAsyncEnumerable<TerminalInputFrame> Input(params TerminalInputFrame[] frames)
    {
        foreach (var frame in frames)
        {
            await Task.Delay(50);
            yield return frame;
        }

        await Task.Delay(300);
    }

    public async ValueTask DisposeAsync()
    {
        if (syslog is not null) await syslog.DisposeAsync();
        if (otlp is not null) await otlp.DisposeAsync();
        syslogCertificate?.Dispose();
        otlpCertificate?.Dispose();
        fixture?.Dispose();
        pki.Dispose();
    }

    private sealed class AllowAll : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null));
    }

    private sealed class ProductionRegistration : IEnvironmentRegistrationStore
    {
        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult<EnvironmentRegistration?>(EnvironmentRegistration.Create(environmentId, "prd", "prd-host", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production));
    }

    private sealed class RunningContainer : IContainerSearchStore
    {
        public Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult<ContainerInventory?>(new ContainerInventory(containerId, "web", "nginx", "running", "r", DateTimeOffset.UtcNow));
    }
}
