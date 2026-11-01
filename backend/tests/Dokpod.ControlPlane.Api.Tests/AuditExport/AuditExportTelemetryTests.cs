using System.Diagnostics;
using System.Net;
using Dokpod.ControlPlane.Api;
using Dokpod.ControlPlane.Api.AuditExport;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T038: com o pipeline de métricas e traces da API (o mesmo de <c>ApiTelemetry</c>) e os transportes como o
/// módulo os registra, um envio OTLP, um envio syslog e a resolução do nome do destino não deixam o host do SIEM em nenhuma dimensão de métrica nem em span. Um
/// pedido HTTP comum continua gerando span e métrica, para mostrar que o filtro é só da exportação.
/// </summary>
public sealed class AuditExportTelemetryTests : IAsyncLifetime
{
    // Nomes e caminho só deste teste: os listeners são do processo e outros testes rodam em paralelo.
    private const string Host = "telemetria.test";
    private const string Path = "/telemetria/v1/logs";
    private const string DnsName = "telemetria-dns.invalid";
    private static readonly Guid Installation = Guid.Parse("5a0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6");
    private readonly TestPki pki = new();
    private readonly string root = Directory.CreateTempSubdirectory("dokpod-027-telemetry-").FullName;
    private readonly List<Activity> spans = [];
    private readonly MetricCollector metrics = new();
    private System.Security.Cryptography.X509Certificates.X509Certificate2? certificate;
    private OtlpTestReceiver? receiver;
    private SyslogTlsTestServer? syslog;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        certificate = pki.Issue(Host, server: true);
        receiver = await OtlpTestReceiver.StartAsync(certificate, Ct);
        syslog = SyslogTlsTestServer.Tls(certificate);
        TestPki.WriteSecret(root, "otel", pki.CaPem);
    }

    [Fact]
    public async Task ExportTraffic_LeavesNoDestinationHost_InMetricsOrSpans()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddDokpodApiMetrics().AddReader(new BaseExportingMetricReader(metrics)).Build();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder().AddDokpodApiTracing().AddInMemory(spans).Build();
        var resolver = new FixedResolver(IPAddress.Loopback);
        var secrets = new MountedSecretReader(root);

        Assert.True((await new UnobservedAuditExportTransport(new OtlpHttpTransport(resolver, secrets)).SendAsync(Destination(AuditExportProtocols.Otlp, receiver!.Port, Path), [Record()], Ct)).Delivered);
        Assert.True((await new UnobservedAuditExportTransport(new SyslogTlsTransport(resolver, secrets)).SendAsync(Destination(AuditExportProtocols.Syslog, syslog!.Port, null), [Record()], Ct)).Delivered);
        // O nome não existe; a consulta em si já registraria o nome na métrica de DNS.
        Assert.False((await new DnsDestinationAddressResolver(new DestinationAddressPolicy()).ResolveAsync(DnsName, Ct)).IsAllowed);

        // Controle: um pedido comum ao mesmo receptor gera span e métrica normalmente.
        using (var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
        using (var client = new HttpClient(handler))
        {
            await client.PostAsync(new Uri($"https://127.0.0.1:{receiver.Port}/controle"), new ByteArrayContent([]), Ct);
        }

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        // Cópias sob lock: os listeners são do processo, e spans de outros testes em paralelo chegam a qualquer momento.
        Activity[] spanSnapshot;
        lock (spans) spanSnapshot = [.. spans];
        var tags = metrics.Snapshot();

        // Só spans de cliente: o receptor de teste é um Kestrel neste processo e tem os próprios spans de servidor.
        var spanText = string.Join('\n', spanSnapshot.Where(span => span.Kind == ActivityKind.Client).Select(span => span.DisplayName + " " + string.Join(' ', span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))));
        Assert.Contains("/controle", spanText);
        Assert.DoesNotContain(Host, spanText);
        Assert.DoesNotContain(Path, spanText);

        Assert.Contains(tags, tag => tag.StartsWith("http.client.request.duration:", StringComparison.Ordinal));
        Assert.Contains(tags, tag => tag.StartsWith("dns.lookup.duration:", StringComparison.Ordinal));
        // As métricas do Kestrel do receptor de teste têm endereço próprio; o que importa são as do lado cliente.
        var clientTags = tags.Where(tag => tag.StartsWith("http.client.", StringComparison.Ordinal) || tag.StartsWith("dns.", StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(clientTags, tag => tag.Contains("server.address", StringComparison.Ordinal) || tag.Contains("dns.question.name", StringComparison.Ordinal));
        Assert.DoesNotContain(tags, tag => tag.Contains("telemetria", StringComparison.Ordinal));
    }

    private static AuditExportDestination Destination(string protocol, int port, string? path) => new(
        Guid.NewGuid(), "telemetria", protocol, Host, port, path, "otel", ["audit_export", "containers"], true, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static AuditExportRecord Record() => new(
        Guid.NewGuid(), DateTimeOffset.UtcNow, Installation, Guid.NewGuid(), Guid.NewGuid(),
        "user", "user-1", "Maria", "container.restart", "containers", Guid.NewGuid(), "web-1", "succeeded", null, new Dictionary<string, string>(), Test: false);

    public async ValueTask DisposeAsync()
    {
        if (receiver is not null) await receiver.DisposeAsync();
        if (syslog is not null) await syslog.DisposeAsync();
        certificate?.Dispose();
        pki.Dispose();
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Guarda <c>instrumento:chave=valor</c> de cada ponto exportado.</summary>
    private sealed class MetricCollector : BaseExporter<Metric>
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
                        tags.Add($"{metric.Name}:");
                        foreach (var tag in point.Tags) tags.Add($"{metric.Name}:{tag.Key}={tag.Value}");
                    }
                }
            }

            return ExportResult.Success;
        }
    }
}

internal static class InMemorySpans
{
    public static TracerProviderBuilder AddInMemory(this TracerProviderBuilder builder, List<Activity> spans) =>
        builder.AddProcessor(new SimpleActivityExportProcessor(new SpanCollector(spans)));

    private sealed class SpanCollector(List<Activity> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch) lock (spans) spans.Add(activity);
            return ExportResult.Success;
        }
    }
}
