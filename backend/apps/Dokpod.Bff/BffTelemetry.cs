using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dokpod.Bff;

/// <summary>
/// Exportação OTLP de métricas e traces do BFF (spec 005, gate de observabilidade): login, callback, logout e
/// relay aparecem em `http.server.request.duration` por rota, e o trace segue para a API pelo `traceparent`.
/// Mesma regra da API: só exporta com `OTEL_EXPORTER_OTLP_*` definido; health checks não geram trace.
/// </summary>
internal static class BffTelemetry
{
    public const string ServiceName = "dokpod-bff";

    public static void AddDokpodTelemetry(this WebApplicationBuilder builder)
    {
        var metrics = Enabled(builder.Configuration, "METRICS");
        var traces = Enabled(builder.Configuration, "TRACES");
        if (!metrics && !traces) return;

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                ServiceName,
                serviceVersion: typeof(BffTelemetry).Assembly.GetName().Version?.ToString()));
        if (metrics)
        {
            telemetry.WithMetrics(meters => meters
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(options => Configure(options, builder.Configuration, "METRICS")));
        }

        if (traces)
        {
            telemetry.WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options => Configure(options, builder.Configuration, "TRACES")));
        }
    }

    private static bool Enabled(IConfiguration configuration, string signal) =>
        !string.Equals(configuration["OTEL_SDK_DISABLED"], "true", StringComparison.OrdinalIgnoreCase) &&
        (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]) ||
            !string.IsNullOrWhiteSpace(configuration[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"]));

    /// <summary>
    /// `AddOtlpExporter` por sinal só lê as variáveis gerais; o endpoint e o protocolo do próprio sinal
    /// (`OTEL_EXPORTER_OTLP_{SINAL}_*`) são aplicados aqui, senão o exportador cai em gRPC em localhost:4317.
    /// </summary>
    private static void Configure(OpenTelemetry.Exporter.OtlpExporterOptions options, IConfiguration configuration, string signal)
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
