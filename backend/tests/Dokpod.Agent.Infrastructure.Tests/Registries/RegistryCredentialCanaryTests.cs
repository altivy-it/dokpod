using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Images;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Registries;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>
/// Spec 025 T024 (SC-003), lado do agente: a senha plantada no <c>config.json</c> do host vai ao serviço de token do
/// registro e a nenhum outro lugar. Os resultados do cliente só levam o motivo, o <c>ToString</c> da credencial mascara
/// o segredo e nenhum span ou métrica do <c>System.Net.Http</c> neste processo leva a senha ou o token emitido. O
/// caminho completo até o plano de controle está em <c>ImageUpdateCanaryTests</c> (Api.Tests).
/// </summary>
public sealed class RegistryCredentialCanaryTests : IAsyncLifetime
{
    // Montada por concatenação para não parecer credencial literal na varredura de segredos.
    private static readonly string PasswordCanary = "canario025" + "agente";
    private readonly RegistryTestPki pki = new();
    private readonly string root = Directory.CreateTempSubdirectory("dokpod-025-agent-canary-").FullName;
    private readonly List<string> telemetry = [];
    private X509Certificate2? certificate;
    private OciTestRegistry? registry;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        certificate = pki.IssueServer();
        registry = await OciTestRegistry.StartAsync(certificate, Ct);
        registry.Auth = OciTestAuth.Bearer;
        registry.Credential = PasswordCanary;
    }

    [Fact]
    public async Task PlantedPassword_GoesOnlyToTheTokenService()
    {
        using var spans = ListenToSpans();
        using var metrics = ListenToMetrics();
        var credentials = new RegistryCredentialFile(WriteConfig());
        var digests = Enumerable.Range(0, 16).Select(i => registry!.PutManifest($"app{i}", "1.0", "linux/amd64")).ToArray();

        // Uma instância do cliente para 16 repositórios em paralelo, como o handler faz com 4 consultas simultâneas.
        var client = Client(credentials);
        var heads = new ManifestHead[digests.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, digests.Length), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = Ct },
            async (i, token) => heads[i] = await client.HeadAsync(Reference($"app{i}:1.0"), token));
        Assert.Equal(digests, heads.Select(head => head.Digest));
        Assert.Contains(registry!.Requests, r => r.Path == "/token" && r.Authorization == Basic(registry.Username, PasswordCanary));

        registry.Credential = "outra-" + "senha";
        var denied = await Client(credentials).HeadAsync(Reference("app0:1.0"), Ct);
        Assert.Equal(ImageUpdateReasons.Unauthorized, denied.FailureReason);

        var outputs = new List<string>
        {
            credentials.CredentialFor(registry.Registry)!.ToString(),
            string.Join('\n', heads.Select(head => head.ToString())),
            denied.ToString(),
        };
        lock (telemetry) outputs.AddRange(telemetry);
        Assert.Contains(outputs, line => line.Contains("/v2/app0/manifests/1.0", StringComparison.Ordinal)); // os spans foram vistos
        foreach (var canary in new[] { PasswordCanary, Convert.ToBase64String(Encoding.UTF8.GetBytes($"{registry.Username}:{PasswordCanary}")), "token-autenticado" })
        {
            Assert.DoesNotContain(outputs, line => line.Contains(canary, StringComparison.Ordinal));
        }
    }

    private string WriteConfig()
    {
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{registry!.Username}:{PasswordCanary}"));
        var path = Path.Combine(root, "config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { auths = new Dictionary<string, object> { [registry.Registry] = new { auth } } }));
        return path;
    }

    private ActivityListener ListenToSpans()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("System.Net", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var text = activity.DisplayName + " " + string.Join(' ', activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));
                lock (telemetry) telemetry.Add(text);
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private MeterListener ListenToMetrics()
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meter) =>
            {
                if (instrument.Meter.Name.StartsWith("System.Net", StringComparison.Ordinal)) meter.EnableMeasurementEvents(instrument);
            },
        };
        void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            var text = new StringBuilder(instrument.Name);
            foreach (var tag in tags) text.Append($" {tag.Key}={tag.Value}");
            lock (telemetry) telemetry.Add(text.ToString());
        }

        listener.SetMeasurementEventCallback<double>(Record);
        listener.SetMeasurementEventCallback<long>(Record);
        listener.SetMeasurementEventCallback<int>(Record);
        listener.Start();
        return listener;
    }

    private ImageReference Reference(string repositoryAndTag)
    {
        Assert.True(ImageReference.TryParse($"{registry!.Registry}/{repositoryAndTag}", out var reference));
        return reference;
    }

    private OciDistributionClient Client(RegistryCredentialFile credentials)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck },
            },
        };
        handler.SslOptions.CertificateChainPolicy!.CustomTrustStore.Add(pki.Ca);
        return new OciDistributionClient(new HttpMessageInvoker(handler), credentials.CredentialFor);
    }

    private static string Basic(string user, string secret) => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{secret}"));

    public async ValueTask DisposeAsync()
    {
        if (registry is not null) await registry.DisposeAsync();
        certificate?.Dispose();
        pki.Dispose();
        Directory.Delete(root, recursive: true);
    }
}
