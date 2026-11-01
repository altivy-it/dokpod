using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dokpod.ControlPlane.Application.AuditExport;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>Tempos limite e corpo de resposta lido (threat model L2).</summary>
public sealed record OtlpHttpTransportOptions
{
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxResponseBytes { get; init; } = 64 * 1024;
}

/// <summary>
/// OTLP/HTTP com protobuf (spec 027 D-005, contracts/export-record.md): <c>POST {path}</c> com um
/// <c>ExportLogsServiceRequest</c> por lote. TLS 1.2/1.3 com cadeia e nome conferidos; o socket conecta ao IP aprovado
/// pela <see cref="DestinationAddressPolicy"/> (L3), sem proxy e sem seguir redirecionamento (L1). O token do secret é
/// relido a cada envio (L9). 2xx confirma o lote; 4xx que não seja 408 ou 429 é <c>rejected</c>; o resto tenta de novo
/// com a espera do remetente. Nenhum código carrega mensagem do servidor.
/// </summary>
public sealed class OtlpHttpTransport(
    IDestinationAddressResolver resolver,
    IAuditExportSecretReader secrets,
    OtlpHttpTransportOptions? options = null) : IAuditExportTransport
{
    public const string ContentType = "application/x-protobuf";

    private readonly OtlpHttpTransportOptions options = options ?? new OtlpHttpTransportOptions();

    public string Protocol => AuditExportProtocols.Otlp;

    public async Task<AuditExportSendResult> SendAsync(AuditExportDestination destination, IReadOnlyList<AuditExportRecord> batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(batch);
        var secret = secrets.Read(destination.SecretRef);
        if (secret.ErrorCode is not null) return AuditExportSendResult.Failed(AuditExportErrorCodes.SecretMissing);

        var addresses = await resolver.ResolveAsync(destination.Host, cancellationToken).ConfigureAwait(false);
        if (!addresses.IsAllowed) return AuditExportSendResult.Failed(addresses.ErrorCode ?? AuditExportErrorCodes.AddressBlocked);

        using var clientCertificate = LoadClientCertificate(secret);
        using var handler = Handler(addresses.Allowed, secret.CaPem, clientCertificate);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.SendTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(destination))
        {
            Content = new ByteArrayContent(ToRequest(batch).ToByteArray()),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
        if (secret.Token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret.Token);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            await DrainAsync(response, timeout.Token).ConfigureAwait(false);
            return Classify(response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuditExportSendResult.Failed(AuditExportErrorCodes.Timeout);
        }
        catch (HttpRequestException exception)
        {
            return AuditExportSendResult.Failed(exception.HttpRequestError == HttpRequestError.SecureConnectionError || exception.InnerException is AuthenticationException
                ? AuditExportErrorCodes.TlsError
                : AuditExportErrorCodes.ConnectionRefused);
        }
        catch (IOException)
        {
            return AuditExportSendResult.Failed(AuditExportErrorCodes.ConnectionRefused);
        }
    }

    /// <summary>
    /// 2xx confirma; 3xx não é seguido e, como 4xx que não seja 408 ou 429, vira <c>rejected</c>; 408 e 504 viram
    /// <c>timeout</c>; 429 e os demais 5xx, <c>connection_refused</c> (destino indisponível).
    /// </summary>
    public static AuditExportSendResult Classify(HttpStatusCode status) => (int)status switch
    {
        >= 200 and < 300 => AuditExportSendResult.Ok,
        408 or 504 => AuditExportSendResult.Failed(AuditExportErrorCodes.Timeout),
        429 or >= 500 => AuditExportSendResult.Failed(AuditExportErrorCodes.ConnectionRefused),
        _ => AuditExportSendResult.Failed(AuditExportErrorCodes.Rejected),
    };

    /// <summary>Lote como <c>ExportLogsServiceRequest</c>: um <c>Resource</c>, um <c>ScopeLogs</c> e um <c>LogRecord</c> por evento.</summary>
    internal static ExportLogsServiceRequest ToRequest(IReadOnlyList<AuditExportRecord> batch)
    {
        var scope = new ScopeLogs { Scope = new InstrumentationScope { Name = "dokpod.audit" } };
        scope.LogRecords.AddRange(batch.Select(ToLogRecord));
        var resource = new Resource();
        resource.Attributes.Add(Attribute("service.name", "dokpod"));
        if (batch.Count > 0) resource.Attributes.Add(Attribute("dokpod.installation.id", batch[0].InstallationId.ToString("D")));
        var resourceLogs = new ResourceLogs { Resource = resource };
        resourceLogs.ScopeLogs.Add(scope);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resourceLogs);
        return request;
    }

    private static LogRecord ToLogRecord(AuditExportRecord record)
    {
        var log = new LogRecord
        {
            TimeUnixNano = (ulong)(record.OccurredAtUtc - DateTimeOffset.UnixEpoch).Ticks * 100,
            SeverityNumber = record.IsWarning ? SeverityNumber.Warn : SeverityNumber.Info,
            SeverityText = record.IsWarning ? "WARN" : "INFO",
            Body = new AnyValue { StringValue = record.Action },
        };
        log.Attributes.Add(Attribute("dokpod.event.id", record.EventId.ToString("D")));
        log.Attributes.Add(Attribute("dokpod.correlation.id", record.CorrelationId.ToString("D")));
        AddIfPresent(log, "dokpod.command.id", record.CommandId?.ToString("D"));
        log.Attributes.Add(Attribute("dokpod.actor.kind", record.ActorKind));
        log.Attributes.Add(Attribute("dokpod.actor.id", record.ActorId));
        AddIfPresent(log, "dokpod.actor.name", record.ActorName);
        AddIfPresent(log, "dokpod.action.category", record.Category);
        AddIfPresent(log, "dokpod.environment.id", record.EnvironmentId == Guid.Empty ? null : record.EnvironmentId.ToString("D"));
        AddIfPresent(log, "dokpod.subject.id", record.SubjectId);
        log.Attributes.Add(Attribute("dokpod.outcome", record.Outcome));
        AddIfPresent(log, "dokpod.failure.code", record.FailureCode);
        log.Attributes.Add(Attribute("dokpod.details", record.DetailsJson()));
        log.Attributes.Add(new KeyValue { Key = "dokpod.test", Value = new AnyValue { BoolValue = record.Test } });
        return log;
    }

    private static void AddIfPresent(LogRecord log, string key, string? value)
    {
        if (value is not null) log.Attributes.Add(Attribute(key, value));
    }

    private static KeyValue Attribute(string key, string value) => new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static Uri Endpoint(AuditExportDestination destination)
    {
        var host = IPAddress.TryParse(destination.Host, out var literal) && literal.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{destination.Host}]"
            : destination.Host;
        return new Uri($"https://{host}:{destination.Port}{destination.Path ?? AuditExportProtocols.DefaultOtlpPath}", UriKind.Absolute);
    }

    private SocketsHttpHandler Handler(IReadOnlyList<IPAddress> addresses, string? caPem, X509Certificate2? clientCertificate)
    {
        var ssl = new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            // Sem callback: qualquer erro de cadeia ou nome recusa a conexão.
        };
        if (caPem is not null)
        {
            var trust = new X509Certificate2Collection();
            trust.ImportFromPem(caPem);
            var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
            policy.CustomTrustStore.AddRange(trust);
            ssl.CertificateChainPolicy = policy;
        }

        if (clientCertificate is not null) ssl.ClientCertificates = [clientCertificate];
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = options.ConnectTimeout,
            PooledConnectionLifetime = TimeSpan.Zero,
            SslOptions = ssl,
            ConnectCallback = async (context, cancellationToken) =>
            {
                // O nome fica para SNI e validação; a conexão vai só ao IP já aprovado (L3).
                SocketException? last = null;
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (SocketException exception)
                    {
                        socket.Dispose();
                        last = exception;
                    }
                }

                throw last ?? new SocketException((int)SocketError.HostUnreachable);
            },
        };
    }

    /// <summary>Lê e descarta no máximo <see cref="OtlpHttpTransportOptions.MaxResponseBytes"/> do corpo (L2).</summary>
    private async Task DrainAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        var total = 0;
        int read;
        while (total < options.MaxResponseBytes && (read = await body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, options.MaxResponseBytes - total)), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
        }
    }

    private static X509Certificate2? LoadClientCertificate(AuditExportSecret secret)
    {
        if (secret.ClientCertificatePem is null || secret.ClientKeyPem is null) return null;
        using var ephemeral = X509Certificate2.CreateFromPem(secret.ClientCertificatePem, secret.ClientKeyPem);
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }
}
