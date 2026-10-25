using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dokpod.ControlPlane.Api;

/// <summary>
/// Exportação OTLP de métricas e traces (spec 005, gate de observabilidade). Configurada só pelas variáveis
/// padrão `OTEL_EXPORTER_OTLP_*`: cada sinal liga quando há endpoint geral ou do próprio sinal; sem nenhum,
/// nada é exportado. Query strings já saem redigidas pela instrumentação; health checks não geram trace.
/// </summary>
internal static class ApiTelemetry
{
    public const string ServiceName = "dokpod-api";

    public static void AddDokpodTelemetry(this WebApplicationBuilder builder)
    {
        var metrics = TelemetrySignals.Enabled(builder.Configuration, "METRICS");
        var traces = TelemetrySignals.Enabled(builder.Configuration, "TRACES");
        if (!metrics && !traces) return;

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                ServiceName,
                serviceVersion: typeof(ApiTelemetry).Assembly.GetName().Version?.ToString()));
        if (metrics)
        {
            telemetry.WithMetrics(meters => meters
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ControlPlaneMetrics.MeterName, AuditEventMetrics.MeterName, "Npgsql")
                .AddOtlpExporter(options => TelemetrySignals.Configure(options, builder.Configuration, "METRICS")));
        }

        if (traces)
        {
            telemetry.WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql")
                .AddOtlpExporter(options => TelemetrySignals.Configure(options, builder.Configuration, "TRACES")));
        }
    }
}

internal static class TelemetrySignals
{
    public static bool Enabled(IConfiguration configuration, string signal) =>
        !string.Equals(configuration["OTEL_SDK_DISABLED"], "true", StringComparison.OrdinalIgnoreCase) &&
        (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]) ||
            !string.IsNullOrWhiteSpace(configuration[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"]));

    /// <summary>
    /// `AddOtlpExporter` por sinal só lê as variáveis gerais; o endpoint e o protocolo do próprio sinal
    /// (`OTEL_EXPORTER_OTLP_{SINAL}_*`) são aplicados aqui, senão o exportador cai em gRPC em localhost:4317.
    /// </summary>
    public static void Configure(OpenTelemetry.Exporter.OtlpExporterOptions options, IConfiguration configuration, string signal)
    {
        var endpoint = configuration[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(endpoint)) options.Endpoint = new Uri(endpoint);
        var protocol = configuration[$"OTEL_EXPORTER_OTLP_{signal}_PROTOCOL"] ?? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        if (protocol is not null)
        {
            options.Protocol = protocol.Trim().Equals("grpc", StringComparison.OrdinalIgnoreCase)
                ? OpenTelemetry.Exporter.OtlpExportProtocol.Grpc
                : OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        }
    }
}
