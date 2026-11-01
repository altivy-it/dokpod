using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Receptor OTLP/HTTP em processo (Kestrel com HTTPS em <c>127.0.0.1</c>): guarda cada <c>ExportLogsServiceRequest</c>
/// e o cabeçalho <c>Authorization</c>, e responde com o status configurado, um redirecionamento, atraso ou corpo grande.
/// </summary>
internal sealed class OtlpTestReceiver : IAsyncDisposable
{
    private readonly WebApplication app;

    private OtlpTestReceiver(WebApplication app)
    {
        this.app = app;
    }

    public ConcurrentQueue<(ExportLogsServiceRequest Request, string? Authorization, string? ContentType, string Path)> Received { get; } = new();

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int ResponseBytes { get; set; }

    public int Port => new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

    public static async Task<OtlpTestReceiver> StartAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        var app = builder.Build();
        var receiver = new OtlpTestReceiver(app);
        app.MapPost("/{**path}", async (HttpContext context) =>
        {
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body, context.RequestAborted);
            receiver.Received.Enqueue((
                ExportLogsServiceRequest.Parser.ParseFrom(body.ToArray()),
                context.Request.Headers.Authorization.ToString() is { Length: > 0 } authorization ? authorization : null,
                context.Request.ContentType,
                context.Request.Path.Value ?? string.Empty));
            if (receiver.Delay > TimeSpan.Zero) await Task.Delay(receiver.Delay, context.RequestAborted);
            context.Response.StatusCode = (int)receiver.Status;
            if (receiver.Status is HttpStatusCode.Redirect) context.Response.Headers.Location = "https://127.0.0.1:1/v1/logs";
            if (receiver.ResponseBytes > 0)
            {
                var chunk = new byte[64 * 1024];
                for (var written = 0; written < receiver.ResponseBytes && !context.RequestAborted.IsCancellationRequested; written += chunk.Length)
                {
                    try
                    {
                        await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
                    }
                    catch (Exception exception) when (exception is IOException or OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        });
        await app.StartAsync(cancellationToken);
        return receiver;
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
