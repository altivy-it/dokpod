using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>CA de teste gerada na execução e certificado do servidor para <c>localhost</c> e <c>127.0.0.1</c>.</summary>
internal sealed class RegistryTestPki : IDisposable
{
    private readonly RSA caKey = RSA.Create(2048);

    public RegistryTestPki()
    {
        var request = new CertificateRequest("CN=Dokpod Registry Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Ca = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    public X509Certificate2 Ca { get; }

    public X509Certificate2 IssueServer()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var issued = request.Create(Ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(12));
        using var withKey = issued.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
    }

    public void Dispose()
    {
        Ca.Dispose();
        caKey.Dispose();
    }
}

/// <summary>Modo de autenticação do registro de teste.</summary>
internal enum OciTestAuth
{
    Anonymous,
    Bearer,
    Basic,
}

/// <summary>
/// Registro OCI em processo (Kestrel com HTTPS em 127.0.0.1) para os testes do agente (spec 025 T002): manifestos e índices
/// multiarquitetura, desafio <c>Bearer</c> com <c>realm</c> configurável ou <c>Basic</c>, e respostas forçadas (status,
/// redirecionamento, atraso e corpos grandes). Guarda cada pedido recebido com o <c>Authorization</c> enviado.
/// </summary>
internal sealed class OciTestRegistry : IAsyncDisposable
{
    public const string IndexType = "application/vnd.oci.image.index.v1+json";
    public const string ManifestType = "application/vnd.oci.image.manifest.v1+json";

    private readonly WebApplication app;
    private readonly ConcurrentDictionary<string, (string MediaType, byte[] Body)> manifests = new(StringComparer.Ordinal);

    private OciTestRegistry(WebApplication app)
    {
        this.app = app;
    }

    public ConcurrentQueue<(string Method, string Path, string? Authorization)> Requests { get; } = new();

    public OciTestAuth Auth { get; set; } = OciTestAuth.Anonymous;

    /// <summary><c>realm</c> anunciado no desafio <c>Bearer</c>; padrão, o próprio registro em <c>localhost</c>.</summary>
    public string? Realm { get; set; }

    public string Username { get; set; } = "dokpod";

    public string Credential { get; set; } = "senha-de-teste";

    /// <summary>Status forçado nas rotas de manifesto (nulo: comportamento normal).</summary>
    public HttpStatusCode? ManifestStatus { get; set; }

    /// <summary>Redireciona o manifesto para esta URL (sempre 307).</summary>
    public string? ManifestRedirect { get; set; }

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>Corpo extra, em bytes, no <c>GET</c> do manifesto e na resposta do token.</summary>
    public int PaddingBytes { get; set; }

    /// <summary>Omite <c>Docker-Content-Digest</c> nas respostas de manifesto.</summary>
    public bool OmitDigest { get; set; }

    public int Port => new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

    /// <summary>Registro como aparece numa referência de imagem (<c>localhost:porta</c>).</summary>
    public string Registry => $"localhost:{Port}";

    public static async Task<OciTestRegistry> StartAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        var app = builder.Build();
        var registry = new OciTestRegistry(app);
        app.Map("/token", registry.TokenAsync);
        app.Map("/v2/{**path}", registry.ManifestAsync);
        await app.StartAsync(cancellationToken);
        return registry;
    }

    /// <summary>Publica um manifesto de imagem simples e devolve o seu digest.</summary>
    public string PutManifest(string repository, string tag, string platformLabel)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = ManifestType,
            config = new { mediaType = "application/vnd.oci.image.config.v1+json", digest = Digest(Encoding.UTF8.GetBytes(platformLabel)), size = platformLabel.Length },
            layers = Array.Empty<object>(),
        });
        return Put(repository, tag, ManifestType, body);
    }

    /// <summary>Publica um índice com um manifesto por plataforma (<c>os/arquitetura[/variante]</c> → digest) e devolve o seu digest.</summary>
    public string PutIndex(string repository, string tag, IReadOnlyDictionary<string, string> platforms)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = IndexType,
            manifests = platforms.Select(entry =>
            {
                var parts = entry.Key.Split('/');
                return new Dictionary<string, object>
                {
                    ["mediaType"] = ManifestType,
                    ["digest"] = entry.Value,
                    ["size"] = 100,
                    ["platform"] = parts.Length == 3
                        ? new Dictionary<string, string> { ["os"] = parts[0], ["architecture"] = parts[1], ["variant"] = parts[2] }
                        : new Dictionary<string, string> { ["os"] = parts[0], ["architecture"] = parts[1] },
                };
            }).ToArray(),
        });
        return Put(repository, tag, IndexType, body);
    }

    public void Remove(string repository, string tag) => manifests.TryRemove($"{repository}:{tag}", out _);

    public static string Digest(byte[] content) => "sha256:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private string Put(string repository, string tag, string mediaType, byte[] body)
    {
        var digest = Digest(body);
        manifests[$"{repository}:{tag}"] = (mediaType, body);
        manifests[$"{repository}:{digest}"] = (mediaType, body);
        return digest;
    }

    private async Task TokenAsync(HttpContext context)
    {
        Requests.Enqueue((context.Request.Method, context.Request.Path.Value ?? string.Empty, Header(context)));
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, context.RequestAborted);
        var basic = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Credential}"));
        var authorized = Header(context) is not { } header || header == basic;
        if (!authorized)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var token = Header(context) is null ? "token-anonimo" : "token-autenticado";
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { token, padding = new string('x', PaddingBytes) }), context.RequestAborted);
    }

    private async Task ManifestAsync(HttpContext context)
    {
        Requests.Enqueue((context.Request.Method, context.Request.Path.Value ?? string.Empty, Header(context)));
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, context.RequestAborted);
        if (!Authorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = Auth == OciTestAuth.Basic
                ? "Basic realm=\"dokpod-test\""
                : $"Bearer realm=\"{Realm ?? $"https://localhost:{Port}/token"}\",service=\"dokpod-test\",scope=\"repository:app:pull\"";
            return;
        }

        if (ManifestRedirect is not null)
        {
            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers.Location = ManifestRedirect;
            return;
        }

        if (ManifestStatus is { } status)
        {
            context.Response.StatusCode = (int)status;
            return;
        }

        // /v2/<repositório>/manifests/<tag ou digest>
        var path = (string)context.Request.RouteValues["path"]!;
        var separator = path.LastIndexOf("/manifests/", StringComparison.Ordinal);
        if (separator < 0 || !manifests.TryGetValue($"{path[..separator]}:{path[(separator + "/manifests/".Length)..]}", out var manifest))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = manifest.MediaType;
        if (!OmitDigest) context.Response.Headers["Docker-Content-Digest"] = Digest(manifest.Body);
        if (HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.ContentLength = manifest.Body.Length;
            return;
        }

        await context.Response.Body.WriteAsync(manifest.Body, context.RequestAborted);
        if (PaddingBytes > 0)
        {
            var chunk = new byte[64 * 1024];
            Array.Fill(chunk, (byte)' ');
            for (var written = 0; written < PaddingBytes && !context.RequestAborted.IsCancellationRequested; written += chunk.Length)
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
    }

    private bool Authorized(HttpContext context) => Auth switch
    {
        OciTestAuth.Anonymous => true,
        OciTestAuth.Bearer => Header(context) is "Bearer token-anonimo" or "Bearer token-autenticado",
        OciTestAuth.Basic => Header(context) == "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Credential}")),
        _ => false,
    };

    private static string? Header(HttpContext context) =>
        context.Request.Headers.Authorization.ToString() is { Length: > 0 } value ? value : null;

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
