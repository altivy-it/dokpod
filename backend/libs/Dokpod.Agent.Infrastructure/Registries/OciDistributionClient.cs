using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Images;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Domain.Images;

namespace Dokpod.Agent.Infrastructure.Registries;

/// <summary>
/// Cliente da API OCI Distribution (spec 025 T009, D-001 e D-005): <c>HEAD</c> do manifesto com os quatro tipos de
/// <c>Accept</c>, <c>GET</c> do índice só quando preciso, desafio <c>Bearer</c> ou <c>Basic</c> com a credencial do host,
/// TLS sempre verificado, 10 s por requisição, 4 MiB de corpo e redirecionamento só para o mesmo host.
/// Threat model: o <c>realm</c> só é aceito em <c>https</c> e só recebe a credencial quando é do mesmo host do registro
/// ou é <c>auth.docker.io</c> para o Docker Hub (L1); o token fica só nesta instância (L3), e a resposta do serviço de
/// token é lida até 64 KiB (L4). Nada de cabeçalho, token, URL ou corpo sai daqui: só o motivo da falha.
/// </summary>
public sealed class OciDistributionClient(HttpMessageInvoker http, Func<string, RegistryCredential?> credentials, TimeSpan? requestTimeout = null) : IRegistryManifestClient
{
    public const int MaxManifestBytes = 4 * 1024 * 1024;
    public const int MaxTokenBytes = 64 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const int MaxRedirects = 3;

    private readonly TimeSpan timeoutPerRequest = requestTimeout ?? RequestTimeout;

    // O handler consulta até 4 referências ao mesmo tempo com esta instância (T024 achou a corrida com Dictionary).
    private readonly ConcurrentDictionary<string, AuthenticationHeaderValue> authorizations = new(StringComparer.Ordinal);

    /// <summary>Cliente de produção: TLS verificado pelo sistema, proxy das variáveis do container, sem redirecionamento automático.</summary>
    public static OciDistributionClient Create(RegistryCredentialFile credentialFile) =>
        new(new HttpMessageInvoker(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = RequestTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        }, disposeHandler: true), credentialFile.CredentialFor);

    /// <summary>Host da API do registro: o Docker Hub responde em <c>registry-1.docker.io</c>.</summary>
    public static string ApiHost(string registry) => registry == ImageReference.DefaultRegistry ? "registry-1.docker.io" : registry;

    public async Task<ManifestHead> HeadAsync(ImageReference reference, CancellationToken cancellationToken)
    {
        if (reference.Tag is null) return ManifestHead.Failed(ImageUpdateReasons.InvalidResponse);
        var (response, reason) = await SendAsync(reference, HttpMethod.Head, reference.Tag, cancellationToken);
        if (response is null) return ManifestHead.Failed(reason!);
        using (response)
        {
            var digest = response.Headers.TryGetValues("Docker-Content-Digest", out var values) ? values.FirstOrDefault() : null;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            return ContentDigest.IsValid(digest) && mediaType is not null && ManifestMediaTypes.All.Contains(mediaType)
                ? new ManifestHead(digest, mediaType, null)
                : ManifestHead.Failed(ImageUpdateReasons.InvalidResponse);
        }
    }

    public async Task<ManifestIndex> GetIndexAsync(ImageReference reference, string digest, CancellationToken cancellationToken)
    {
        if (!ContentDigest.IsValid(digest)) return ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse);
        var (response, reason) = await SendAsync(reference, HttpMethod.Get, digest, cancellationToken);
        if (response is null) return ManifestIndex.Failed(reason!);
        using (response)
        {
            var body = await ReadLimitedAsync(response, MaxManifestBytes, cancellationToken);
            if (body is null) return ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse);
            try
            {
                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("manifests", out var manifests) || manifests.ValueKind != JsonValueKind.Array)
                {
                    return ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse);
                }

                var entries = new List<(string, string)>();
                foreach (var manifest in manifests.EnumerateArray())
                {
                    if (!manifest.TryGetProperty("digest", out var entryDigest) || !ContentDigest.IsValid(entryDigest.GetString())) continue;
                    if (!manifest.TryGetProperty("platform", out var platform) || platform.ValueKind != JsonValueKind.Object) continue;
                    string? Text(string name) => platform.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    if (Text("os") is not { Length: > 0 } os || Text("architecture") is not { Length: > 0 } architecture) continue;
                    entries.Add((ImagePlatform.Of(os, architecture, Text("variant")), entryDigest.GetString()!));
                }

                return new ManifestIndex(entries, null);
            }
            catch (JsonException)
            {
                return ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse);
            }
        }
    }

    private async Task<(HttpResponseMessage? Response, string? Reason)> SendAsync(
        ImageReference reference, HttpMethod method, string tagOrDigest, CancellationToken cancellationToken)
    {
        var host = ApiHost(reference.Registry);
        var uri = new Uri($"https://{host}/v2/{reference.Repository}/manifests/{tagOrDigest}");
        authorizations.TryGetValue(reference.Name, out var authorization);
        var challenged = false;
        for (var redirects = 0; ;)
        {
            HttpResponseMessage response;
            try
            {
                response = await SendOnceAsync(method, uri, authorization, accept: true, cancellationToken);
            }
            catch (Exception exception) when (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return (null, ImageUpdateReasons.Timeout);
            }
            catch (HttpRequestException)
            {
                return (null, ImageUpdateReasons.RegistryUnavailable);
            }

            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                response.Dispose();
                var target = location.IsAbsoluteUri ? location : new Uri(uri, location);
                // O Authorization nunca segue para outro host (D-005).
                if (++redirects > MaxRedirects || target.Scheme != Uri.UriSchemeHttps || !string.Equals(target.Authority, uri.Authority, StringComparison.OrdinalIgnoreCase))
                {
                    return (null, ImageUpdateReasons.InvalidResponse);
                }

                uri = target;
                continue;
            }

            if (status == 401 && !challenged)
            {
                challenged = true;
                var challenge = response.Headers.WwwAuthenticate.FirstOrDefault();
                response.Dispose();
                var (resolved, reason) = await AuthorizeAsync(reference, host, challenge, cancellationToken);
                if (resolved is null) return (null, reason);
                authorization = resolved;
                authorizations[reference.Name] = resolved;
                continue;
            }

            var failure = status switch
            {
                >= 200 and < 300 => null,
                401 or 403 => ImageUpdateReasons.Unauthorized,
                404 => ImageUpdateReasons.ReferenceNotFound,
                429 => ImageUpdateReasons.RateLimited,
                >= 500 => ImageUpdateReasons.RegistryUnavailable,
                _ => ImageUpdateReasons.InvalidResponse,
            };
            if (failure is null) return (response, null);
            response.Dispose();
            return (null, failure);
        }
    }

    private async Task<(AuthenticationHeaderValue? Authorization, string? Reason)> AuthorizeAsync(
        ImageReference reference, string host, AuthenticationHeaderValue? challenge, CancellationToken cancellationToken)
    {
        var credential = credentials(reference.Registry);
        if (challenge is null) return (null, ImageUpdateReasons.Unauthorized);
        if (string.Equals(challenge.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            return credential is null
                ? (null, ImageUpdateReasons.Unauthorized)
                : (BasicOf(credential), null);
        }

        if (!string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)) return (null, ImageUpdateReasons.Unauthorized);
        var parameters = ParseChallenge(challenge.Parameter);
        if (!parameters.TryGetValue("realm", out var realmText) || !Uri.TryCreate(realmText, UriKind.Absolute, out var realm) ||
            realm.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(realm.UserInfo))
        {
            return (null, ImageUpdateReasons.Unauthorized); // L1: realm só https
        }

        var query = new List<string>();
        if (parameters.TryGetValue("service", out var service)) query.Add("service=" + Uri.EscapeDataString(service));
        query.Add("scope=" + Uri.EscapeDataString($"repository:{reference.Repository}:pull"));
        var tokenUri = new UriBuilder(realm) { Query = string.Join('&', query) }.Uri;
        var sendCredential = credential is not null && (string.Equals(realm.Host, HostOf(host), StringComparison.OrdinalIgnoreCase) ||
            (reference.Registry == ImageReference.DefaultRegistry && string.Equals(realm.Host, "auth.docker.io", StringComparison.OrdinalIgnoreCase)));
        HttpResponseMessage response;
        try
        {
            response = await SendOnceAsync(HttpMethod.Get, tokenUri, sendCredential ? BasicOf(credential!) : null, accept: false, cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return (null, ImageUpdateReasons.Timeout);
        }
        catch (HttpRequestException)
        {
            return (null, ImageUpdateReasons.RegistryUnavailable);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is 401 or 403) return (null, ImageUpdateReasons.Unauthorized);
            if (status == 429) return (null, ImageUpdateReasons.RateLimited);
            if (status >= 500) return (null, ImageUpdateReasons.RegistryUnavailable);
            if (status is < 200 or >= 300) return (null, ImageUpdateReasons.InvalidResponse);
            var body = await ReadLimitedAsync(response, MaxTokenBytes, cancellationToken);
            if (body is null) return (null, ImageUpdateReasons.InvalidResponse);
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                var token = root.TryGetProperty("token", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()
                    : root.TryGetProperty("access_token", out var access) && access.ValueKind == JsonValueKind.String ? access.GetString() : null;
                return string.IsNullOrEmpty(token) ? (null, ImageUpdateReasons.InvalidResponse) : (new AuthenticationHeaderValue("Bearer", token), null);
            }
            catch (JsonException)
            {
                return (null, ImageUpdateReasons.InvalidResponse);
            }
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, Uri uri, AuthenticationHeaderValue? authorization, bool accept, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (authorization is not null) request.Headers.Authorization = authorization;
        if (accept)
        {
            foreach (var mediaType in ManifestMediaTypes.All) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutPerRequest);
        return await http.SendAsync(request, timeout.Token);
    }

    /// <summary>Corpo até <paramref name="limit"/> bytes, dentro do prazo da requisição; nulo se passar do limite, demorar ou falhar.</summary>
    private async Task<byte[]?> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > limit) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutPerRequest);
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > limit) return null;
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception exception) when ((exception is OperationCanceledException && !cancellationToken.IsCancellationRequested) || exception is IOException or HttpRequestException)
        {
            return null;
        }
    }

    private static AuthenticationHeaderValue BasicOf(RegistryCredential credential) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Secret}")));

    private static string HostOf(string authority) => authority.Split(':')[0];

    private static Dictionary<string, string> ParseChallenge(string? parameter)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(parameter)) return values;
        var position = 0;
        while (position < parameter.Length)
        {
            var equals = parameter.IndexOf('=', position);
            if (equals < 0) break;
            var name = parameter[position..equals].Trim(' ', ',');
            position = equals + 1;
            string value;
            if (position < parameter.Length && parameter[position] == '"')
            {
                var end = parameter.IndexOf('"', position + 1);
                if (end < 0) break;
                value = parameter[(position + 1)..end];
                position = end + 1;
            }
            else
            {
                var end = parameter.IndexOf(',', position);
                value = end < 0 ? parameter[position..] : parameter[position..end];
                position = end < 0 ? parameter.Length : end;
            }

            values[name] = value.Trim();
            position = parameter.IndexOf(',', position) is var comma and >= 0 ? comma + 1 : parameter.Length;
        }

        return values;
    }
}
