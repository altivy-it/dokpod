using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Images;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Registries;
using Dokpod.ControlPlane.Api.Tests.AuditExport;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T024 (SC-003): uma senha e um token plantados no <c>config.json</c> do host chegam ao registro (controle
/// positivo), mas não às páginas que o agente envia, aos logs do agente, ao banco, às respostas da API nem aos spans e
/// métricas do pipeline de <see cref="ApiTelemetryInstrumentation"/>. O registro é um Kestrel com HTTPS neste processo:
/// <c>registry.canario.test</c> pede <c>Basic</c> com a senha, e <c>registry-b.canario.test</c> troca o token do
/// <c>config.json</c> por outro, emitido pelo próprio registro, que também não pode vazar.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageUpdateCanaryTests : IAsyncLifetime
{
    private const string BasicHost = "registry.canario.test";
    private const string BearerHost = "registry-b.canario.test";
    private const string Username = "usuario-t024";

    // Montados por concatenação para não parecerem credenciais literais na varredura de segredos.
    private static readonly string PasswordCanary = "canario025" + "senha";
    private static readonly string TokenCanary = "canario025" + "token";
    private static readonly string IssuedCanary = "canario025" + "emitido";
    private static readonly string[] Canaries = [PasswordCanary, TokenCanary, IssuedCanary];
    private static readonly string OldDigest = "sha256:" + new string('a', 64);
    private static readonly string NewDigest = "sha256:" + new string('b', 64);

    private readonly Guid environmentId = Guid.NewGuid();
    private readonly Guid requestId = Guid.NewGuid();
    private readonly ImageUpdateSchedulerTests.ManualTime time = new(DateTimeOffset.UtcNow);
    private readonly TestPki pki = new();
    private readonly string root = Directory.CreateTempSubdirectory("dokpod-025-canary-").FullName;
    private readonly ConcurrentQueue<(string Host, string Path, string? Authorization)> seen = new();
    private readonly List<Activity> spans = [];
    private readonly MetricTags metrics = new();
    private readonly LogLines logs = new();
    private X509Certificate2? basicCertificate;
    private X509Certificate2? bearerCertificate;
    private WebApplication? registry;
    private ControlPlaneDbContext? context;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private int Port => new Uri(registry!.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t025c-{environmentId:N}"[..20], "host-t025c", true, [EnvironmentResourceScopes.Read]), Ct);
        var store = new ImageUpdateStore(context);
        await store.EnsureScheduledAsync([environmentId], time.Now, Ct);
        Assert.True(await store.TryAcquireAsync(environmentId, "replica-t024", time.Now, Ct));
        Assert.True(await store.MarkRequestedAsync(environmentId, "replica-t024", requestId, time.Now, time.Now + ImageUpdateLimits.Interval, Ct));

        basicCertificate = pki.Issue(BasicHost, server: true);
        bearerCertificate = pki.Issue(BearerHost, server: true);
        registry = await StartRegistryAsync();
    }

    [Fact]
    public async Task PlantedPasswordAndToken_ReachOnlyTheRegistry()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddDokpodApiMetrics().AddReader(new BaseExportingMetricReader(metrics)).Build();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().AddDokpodApiTracing().AddInMemory(spans).Build();
        var configPath = WriteConfig();

        var pages = await CheckOnTheAgentAsync(configPath);

        // Controle positivo: a senha e o token do config.json foram ao registro, e o token emitido voltou nele.
        Assert.Contains(seen, r => r is { Host: BasicHost } && r.Authorization == Basic(Username, PasswordCanary));
        Assert.Contains(seen, r => r is { Host: BearerHost, Path: "/token" } && r.Authorization == Basic("oauth2accesstoken", TokenCanary));
        Assert.Contains(seen, r => r is { Host: BearerHost } && r.Authorization == "Bearer " + IssuedCanary);
        var results = pages.SelectMany(page => page.Results).ToDictionary(r => r.Reference, r => (r.State, r.Reason));
        Assert.Equal((ImageUpdateState.UpdateAvailable, ""), results[$"{BasicHost}/app:1.0"]);
        Assert.Equal((ImageUpdateState.UpdateAvailable, ""), results[$"{BearerHost}/app:1.0"]);
        Assert.Equal((ImageUpdateState.Failed, ImageUpdateReasons.ReferenceNotFound), results[$"{BasicHost}/sumiu:1.0"]);
        Assert.Equal((ImageUpdateState.Failed, ImageUpdateReasons.RegistryUnavailable), results[$"{BasicHost}/quebrada:1.0"]);
        Assert.Contains(logs.Snapshot(), line => line.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));

        var ingestion = new ImageUpdateResultIngestion(new ImageUpdateStore(context!), new ImageUpdateTargetReader(context!), time);
        foreach (var page in pages) Assert.Equal(ImageUpdatePageOutcome.Applied, await ingestion.ApplyAsync(environmentId, page, Ct));
        var lookup = (await new ImageUpdateReadService(new ImageUpdateStore(context!), new Licensed()).LoadAsync([environmentId], Ct))!;
        var api = results.Keys.Select(reference => JsonSerializer.Serialize(lookup.ForContainer(environmentId, reference, null, stale: false), JsonSerializerOptions.Web)).ToArray();
        Assert.Contains(api, json => json.Contains("\"update_available\"", StringComparison.Ordinal));

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();
        Activity[] spanSnapshot;
        lock (spans) spanSnapshot = [.. spans];
        var spanText = string.Join('\n', spanSnapshot.Select(span =>
            span.DisplayName + " " + string.Join(' ', span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}")) + " " +
            string.Join(' ', span.Events.SelectMany(e => e.Tags.Select(tag => $"{tag.Key}={tag.Value}")))));
        Assert.Contains(BasicHost, spanText); // o pipeline de fato viu as chamadas ao registro

        var sources = new Dictionary<string, string>
        {
            ["páginas do agente"] = string.Concat(pages.Select(page => page.ToString() + Encoding.UTF8.GetString(Google.Protobuf.MessageExtensions.ToByteArray(page)))),
            ["logs do agente"] = string.Join('\n', logs.Snapshot()),
            ["banco"] = await DumpTablesAsync(),
            ["respostas da API"] = string.Join('\n', api),
            ["spans"] = spanText,
            ["métricas"] = string.Join('\n', metrics.Snapshot()),
        };
        foreach (var (source, text) in sources)
        {
            foreach (var canary in Canaries) Assert.False(text.Contains(canary, StringComparison.Ordinal), $"{canary} apareceu em {source}");
        }
    }

    /// <summary>Mesmo caminho do <c>AgentControlWorker</c>: arquivo de credenciais do host, cliente OCI por pedido e log das falhas.</summary>
    private async Task<List<ImageUpdateCheckResultPage>> CheckOnTheAgentAsync(string configPath)
    {
        var credentials = new RegistryCredentialFile(configPath);
        var logger = logs.CreateLogger("Dokpod.Agent.AgentControlWorker");
        var handler = new ImageUpdateCheckHandler(
            new Engine(),
            () => new OciDistributionClient(new HttpMessageInvoker(LoopbackHandler(), disposeHandler: true), credentials.CredentialFor),
            time,
            (exceptionType, reason) => logger.LogWarning("Image update check failed: {ExceptionType} ({Reason})", exceptionType, reason));
        var request = new ImageUpdateCheckRequest { RequestId = requestId.ToString() };
        foreach (var (reference, image) in Engine.Images) request.Items.Add(new ImageUpdateCheckItem { Reference = reference, ImageId = image });
        var pages = new List<ImageUpdateCheckResultPage>();
        await handler.HandleAsync(request, message =>
        {
            pages.Add(message.ImageUpdateCheckResult);
            return Task.CompletedTask;
        }, Ct);
        return pages;
    }

    private string WriteConfig()
    {
        string Auth(string user, string secret) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{secret}"));
        var config = new Dictionary<string, object>
        {
            ["auths"] = new Dictionary<string, object>
            {
                // O identitytoken nunca é lido: só o par usuário:segredo de `auth` vale para o registro.
                [BasicHost] = new Dictionary<string, string> { ["auth"] = Auth(Username, PasswordCanary), ["identitytoken"] = TokenCanary },
                [BearerHost] = new Dictionary<string, string> { ["auth"] = Auth("oauth2accesstoken", TokenCanary) },
            },
        };
        var path = Path.Combine(root, "config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    private async Task<WebApplication> StartRegistryAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https =>
            https.ServerCertificateSelector = (_, name) => name == BearerHost ? bearerCertificate : basicCertificate)));
        var app = builder.Build();
        app.Run(context =>
        {
            var host = context.Request.Host.Host;
            var path = context.Request.Path.Value ?? string.Empty;
            var authorization = context.Request.Headers.Authorization.ToString();
            seen.Enqueue((host, path, authorization.Length == 0 ? null : authorization));
            if (host == BearerHost && path == "/token")
            {
                if (authorization != Basic("oauth2accesstoken", TokenCanary)) return Status(context, 401);
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(JsonSerializer.Serialize(new { token = IssuedCanary }));
            }

            var authorized = host == BearerHost ? authorization == "Bearer " + IssuedCanary : authorization == Basic(Username, PasswordCanary);
            if (!authorized)
            {
                context.Response.Headers.WWWAuthenticate = host == BearerHost
                    ? $"Bearer realm=\"https://{BearerHost}/token\",service=\"{BearerHost}\""
                    : "Basic realm=\"canario\"";
                return Status(context, 401);
            }

            if (path != "/v2/app/manifests/1.0") return Status(context, 404);
            context.Response.Headers["Docker-Content-Digest"] = NewDigest;
            context.Response.ContentType = ManifestMediaTypes.OciManifest;
            return Task.CompletedTask;
        });
        await app.StartAsync(Ct);
        return app;
    }

    /// <summary>TLS verificado contra a CA de teste, com os dois nomes do registro resolvidos para o Kestrel local.</summary>
    private SocketsHttpHandler LoopbackHandler()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            },
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck },
            },
        };
        handler.SslOptions.CertificateChainPolicy!.CustomTrustStore.Add(pki.Ca);
        return handler;
    }

    private async Task<string> DumpTablesAsync()
    {
        var connection = context!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(Ct);
        var text = new StringBuilder();
        foreach (var table in new[] { "image_update_checks", "image_update_schedules" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT row_to_json(t)::text FROM dokpod.{table} t WHERE t.environment_id = @id";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = environmentId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct)) text.AppendLine(reader.GetString(0));
        }

        Assert.Contains(BasicHost, text.ToString()); // as linhas do host foram gravadas
        return text.ToString();
    }

    private static string Basic(string user, string secret) => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{secret}"));

    private static Task Status(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (registry is not null)
        {
            await registry.StopAsync();
            await registry.DisposeAsync();
        }

        if (context is not null) await context.DisposeAsync();
        basicCertificate?.Dispose();
        bearerCertificate?.Dispose();
        pki.Dispose();
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Imagens locais com <c>RepoDigests</c> antigos; a de <c>quebrada</c> falha com o segredo na mensagem da exceção.</summary>
    private sealed class Engine : IContainerEngine
    {
        public static readonly IReadOnlyDictionary<string, string> Images = new Dictionary<string, string>
        {
            [$"{BasicHost}/app:1.0"] = "sha256:" + new string('1', 64),
            [$"{BearerHost}/app:1.0"] = "sha256:" + new string('2', 64),
            [$"{BasicHost}/sumiu:1.0"] = "sha256:" + new string('3', 64),
            [$"{BasicHost}/quebrada:1.0"] = "sha256:" + new string('4', 64),
        };

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("1", "1.47"));

        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EngineContainer>>([]);

        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);

        public Task<ContainerMutationResult> ExecuteAsync(Dokpod.Domain.Commands.AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerMutationResult(false, "x"));

        public Task<EngineImagePlatform?> InspectImagePlatformAsync(string imageId, CancellationToken cancellationToken)
        {
            var reference = Images.Single(item => item.Value == imageId).Key;
            if (reference.Contains("quebrada", StringComparison.Ordinal)) throw new InvalidOperationException("config.json com " + PasswordCanary);
            return Task.FromResult<EngineImagePlatform?>(new EngineImagePlatform("linux", "amd64", null, [$"{reference[..reference.LastIndexOf(':')]}@{OldDigest}"]));
        }
    }

    private sealed class Licensed : IImageUpdateEntitlement
    {
        public Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    /// <summary>Guarda <c>instrumento:chave=valor</c> de cada ponto exportado.</summary>
    private sealed class MetricTags : BaseExporter<Metric>
    {
        private readonly List<string> tags = [];

        public string[] Snapshot()
        {
            lock (tags) return [.. tags];
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    lock (tags)
                    {
                        foreach (var tag in point.Tags) tags.Add($"{metric.Name}:{tag.Key}={tag.Value}");
                    }
                }
            }

            return ExportResult.Success;
        }
    }

    /// <summary>Provedor de log que guarda a mensagem formatada, os argumentos e a exceção de cada linha.</summary>
    private sealed class LogLines : ILoggerProvider
    {
        private readonly List<string> lines = [];

        public string[] Snapshot()
        {
            lock (lines) return [.. lines];
        }

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(LogLines owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs ? string.Join(' ', pairs.Select(p => $"{p.Key}={p.Value}")) : string.Empty;
                lock (owner.lines) owner.lines.Add($"{logLevel} {formatter(state, exception)} {values} {exception}");
            }
        }
    }
}
