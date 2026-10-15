using System.Net;
using System.Text;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Registries;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Registries;

/// <summary>Spec 020 T003-T004: configuração de registros do Docker e do Podman e credenciais só como indicação.</summary>
public sealed class RegistryAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private const string Canary = "senha-canario-020";

    [Fact]
    public async Task DockerClient_ReadsIndexConfigsAndHubMirrorsWhenLibpodIsAbsent()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}"""),
            "/v1.47/info" => Json("""
                {"RegistryConfig":{"InsecureRegistryCIDRs":["127.0.0.0/8"],"Mirrors":["https://mirror.gcr.io/"],
                 "IndexConfigs":{"docker.io":{"Name":"docker.io","Mirrors":[],"Secure":true,"Official":true},
                                 "registry.local:5000":{"Name":"registry.local:5000","Mirrors":[],"Secure":false,"Official":false}}}}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        var config = await client.GetRegistryConfigAsync(TestContext.Current.CancellationToken);

        Assert.True(config.ConfigAvailable);
        Assert.Equal(["docker.io", "registry.local:5000"], config.Registries.Select(r => r.Registry));
        Assert.Equal(["mirror.gcr.io"], config.Registries[0].Mirrors);
        Assert.True(config.Registries[1].Insecure);
        Assert.False(config.Registries[0].Insecure);
    }

    [Fact]
    public async Task DockerClient_ReadsPodmanRegistriesConfFromLibpodAndOnlyCredentialNames()
    {
        var auth = Path.GetTempFileName();
        try
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("usuario:" + Canary));
            await File.WriteAllTextAsync(auth, $$"""{"auths":{"https://index.docker.io/v1/":{"auth":"{{encoded}}"},"Registry.Empresa.com":{"auth":"{{encoded}}"},"semauth.io":{} } }""", TestContext.Current.CancellationToken);
            var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
            {
                "/version" => Json("""{"Version":"5.2.3","ApiVersion":"1.41","MinAPIVersion":"1.24"}"""),
                "/libpod/info" => Json("""
                    {"registries":{"search":["docker.io","quay.io"],
                      "docker.io":{"Prefix":"docker.io","Location":"docker.io","Insecure":false,"Blocked":false,"Mirrors":[{"Location":"mirror.empresa.com","Insecure":false}]},
                      "registry.local:5000":{"Prefix":"registry.local:5000","Location":"registry.local:5000","Insecure":true,"Blocked":false,"Mirrors":null},
                      "proibido.io":{"Prefix":"proibido.io","Location":"proibido.io","Insecure":false,"Blocked":true}}}
                    """),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
            var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") }, new RegistryCredentialFile(auth));

            var config = await client.GetRegistryConfigAsync(TestContext.Current.CancellationToken);
            var message = new RegistryInventoryBuilder(new FixedTime(Now)).Build(config, force: true)!;

            Assert.Equal(["docker.io", "proibido.io", "quay.io", "registry.local:5000"], config.Registries.Select(r => r.Registry));
            Assert.True(config.Registries.Single(r => r.Registry == "proibido.io").Blocked);
            Assert.True(config.Registries.Single(r => r.Registry == "quay.io").Search);
            Assert.True(config.Registries.Single(r => r.Registry == "registry.local:5000").Insecure);
            Assert.Equal(["docker.io", "registry.empresa.com"], config.CredentialRegistries);
            Assert.Equal("docker.io", message.Registries.Single(r => r.Registry == "mirror.empresa.com").MirrorOf);
            Assert.True(message.Registries.Single(r => r.Registry == "registry.empresa.com").Credential);
            Assert.DoesNotContain(Canary, message.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("usuario", message.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(auth);
        }
    }

    [Fact]
    public void Builder_AdvancesRevisionOnlyWhenTheConfigurationChanges()
    {
        var builder = new RegistryInventoryBuilder(new FixedTime(Now));
        var config = new EngineRegistryConfig(true, [new EngineRegistry("quay.io", [], false, false, true)], []);

        var first = builder.Build(config, force: false);
        var unchanged = builder.Build(config, force: false);
        var forced = builder.Build(config, force: true);
        var changed = builder.Build(config with { CredentialRegistries = ["quay.io"] }, force: false);

        Assert.NotNull(first);
        Assert.Null(unchanged);
        Assert.Equal(first!.Revision, forced!.Revision);
        Assert.True(changed!.Revision > first.Revision);
        Assert.True(changed.Registries.Single().Credential);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
