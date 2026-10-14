using System.Net;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Queries;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Queries;

public sealed class ContainerQueryTests
{
    private const string Id = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Canary = "canary-7f3a9c";

    private static readonly string InspectJson = $$"""
        {
          "Id": "{{Id}}",
          "Path": "node",
          "Args": ["dist/server.js", "--db=postgres://app:{{Canary}}-url@db:5432/app"],
          "Config": {
            "User": "app",
            "WorkingDir": "/srv/app",
            "Env": ["DATABASE_PASSWORD={{Canary}}-env", "NODE_ENV=production"],
            "Cmd": ["node", "dist/server.js", "--token={{Canary}}-cmd"],
            "CreateCommand": ["podman", "run", "-e", "API_KEY={{Canary}}-create", "img"],
            "Labels": { "tier": "api", "traefik.http.middlewares.auth.basicauth.users": "admin:{{Canary}}-label" },
            "Healthcheck": { "Test": ["CMD-SHELL", "curl -u admin:{{Canary}}-hc http://localhost/healthz"], "Interval": 15000000000, "Retries": 3, "Timeout": 5000000000 },
            "Tty": false
          },
          "HostConfig": {
            "Binds": ["/etc/ssl/private:/certs:ro"],
            "LogConfig": { "Type": "loki", "Config": { "loki-url": "https://user:{{Canary}}-loki@loki/api", "splunk-token": "{{Canary}}-splunk" } },
            "RestartPolicy": { "Name": "unless-stopped", "MaximumRetryCount": 0 },
            "NanoCpus": 2000000000,
            "Memory": 536870912
          },
          "Mounts": [
            { "Type": "bind", "Source": "/etc/ssl/private", "Destination": "/certs", "RW": false },
            { "Type": "volume", "Name": "uploads", "Source": "/var/lib/docker/volumes/uploads/_data", "Destination": "/srv/app/uploads", "RW": true },
            { "Type": "bind", "Source": "/run/secrets/db", "Destination": "/run/secrets/db", "RW": false }
          ],
          "NetworkSettings": { "Networks": { "rede-pedidos": { "IPAddress": "172.20.0.14" } } }
        }
        """;

    [Fact]
    public void Inspect_RemovesAndMasksEveryCanaryField()
    {
        using var document = JsonDocument.Parse(InspectJson);
        var result = ContainerInspectSanitizer.Inspect(document.RootElement);
        var json = Encoding.UTF8.GetString(result.Json);

        Assert.DoesNotContain(Canary, json, StringComparison.Ordinal);
        Assert.Contains("Config.Env", result.RedactedPaths);
        Assert.Contains("Config.CreateCommand", result.RedactedPaths);
        Assert.Contains("HostConfig.Binds", result.RedactedPaths);
        Assert.DoesNotContain("/etc/ssl/private", json, StringComparison.Ordinal);
        Assert.Contains("rede-pedidos", json, StringComparison.Ordinal);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void Summary_UsesAllowlistAndMasksLabelsHealthcheckAndLogOptions()
    {
        using var document = JsonDocument.Parse(InspectJson);
        var result = ContainerInspectSanitizer.Summary(document.RootElement);
        var json = Encoding.UTF8.GetString(result.Json);
        using var summary = JsonDocument.Parse(json);
        var root = summary.RootElement;

        Assert.DoesNotContain(Canary, json, StringComparison.Ordinal);
        Assert.Equal(2, root.GetProperty("environmentVariableCount").GetInt32());
        Assert.Equal(1, root.GetProperty("secretCount").GetInt32());
        Assert.Equal(2, root.GetProperty("cpuLimitCores").GetDouble());
        Assert.Equal("unless-stopped", root.GetProperty("restartPolicy").GetProperty("name").GetString());
        Assert.Equal(15, root.GetProperty("healthcheck").GetProperty("intervalSeconds").GetDouble());
        Assert.Equal("uploads", root.GetProperty("volumes")[1].GetProperty("name").GetString());
        Assert.Equal(string.Empty, root.GetProperty("volumes")[0].GetProperty("name").GetString());
        Assert.Equal("api", root.GetProperty("labels").GetProperty("tier").GetString());
    }

    [Theory]
    [InlineData("Authorization: Bearer abc.def.ghijkl123")]
    [InlineData("connecting with password=hunter2&user=x")]
    [InlineData("url https://user:pa55w0rd@registry.local/v2")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----MIIEow...-----END RSA PRIVATE KEY-----")]
    public void Masker_HidesKnownSecretPatterns(string line)
    {
        var masked = SecretMasker.Apply(line);
        Assert.Contains(SecretMasker.Mask, masked, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("pa55w0rd", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("dozjgNryP4J3", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_StripsAnsiInfersLevelAndKeepsMarkupAsText()
    {
        var parsed = LogLineParser.Parse("2026-10-10T18:08:02.114000000Z \u001b[31mERROR\u001b[0m health check falhou <script>alert(1)</script>", hasTimestamp: true);

        Assert.Equal(new DateTimeOffset(2026, 10, 10, 18, 8, 2, 114, TimeSpan.Zero), parsed.Timestamp);
        Assert.Equal("ERROR", parsed.Level);
        Assert.Equal("ERROR health check falhou <script>alert(1)</script>", parsed.Text);
        Assert.Equal("WARN", LogLineParser.Parse("""{"level":"warning","msg":"slow"}""", hasTimestamp: false).Level);
        Assert.Equal(string.Empty, LogLineParser.Parse("plain line", hasTimestamp: false).Level);
        Assert.True(Encoding.UTF8.GetByteCount(LogLineParser.Parse(new string('x', 40_000), false).Text) <= LogLineParser.MaxLineBytes);
    }

    [Fact]
    public async Task Logs_DemultiplexStreamsAndRespectMessageSize()
    {
        var payload = new MemoryStream();
        void Frame(byte stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            payload.Write([stream, 0, 0, 0, (byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length]);
            payload.Write(bytes);
        }

        for (var i = 0; i < 3000; i++)
        {
            Frame(i % 2 == 0 ? (byte)1 : (byte)2, $"2026-10-10T18:08:{i % 60:D2}.000000000Z INFO line {i} {new string('y', 200)}\n");
        }

        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"27.3.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}"""),
            var path when path.EndsWith("/json", StringComparison.Ordinal) => Json(InspectJson),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload.ToArray()) },
        });
        var engine = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });
        var query = new ContainerQueryHandler(engine, TimeProvider.System);
        var sent = new List<AgentMessage>();

        await query.HandleAsync(
            new ContainerQueryRequest { QueryId = "q1", Kind = ContainerQueryKind.Logs, ContainerId = Id, Logs = new LogQuery { Tail = 3000 } },
            message => { sent.Add(message); return Task.CompletedTask; },
            TestContext.Current.CancellationToken);

        Assert.All(sent, message => Assert.True(message.CalculateSize() <= ContainerQueryHandler.MaxMessageBytes));
        Assert.True(sent.Count > 1);
        Assert.True(sent[^1].ContainerQueryResponse.Final);
        Assert.Equal(Enumerable.Range(0, sent.Count).Select(i => (uint)i), sent.Select(m => m.ContainerQueryResponse.Sequence));
        var lines = sent.SelectMany(m => m.ContainerQueryResponse.Lines).ToArray();
        Assert.Contains(lines, line => line.Stream == LogStream.Stderr);
        Assert.Equal("INFO", lines[0].Level);
        Assert.True(lines.Sum(l => l.CalculateSize()) <= ContainerQueryHandler.MaxResponseBytes);
    }

    [Fact]
    public async Task Query_RejectsInvalidContainerAndUnknownKind()
    {
        var query = new ContainerQueryHandler(new NullEngine(), TimeProvider.System);
        var sent = new List<AgentMessage>();
        Task Send(AgentMessage m) { sent.Add(m); return Task.CompletedTask; }

        await query.HandleAsync(new ContainerQueryRequest { QueryId = "a", Kind = ContainerQueryKind.Inspect, ContainerId = "../x" }, Send, TestContext.Current.CancellationToken);
        await query.HandleAsync(new ContainerQueryRequest { QueryId = "b", Kind = (ContainerQueryKind)99, ContainerId = Id }, Send, TestContext.Current.CancellationToken);
        await query.HandleAsync(new ContainerQueryRequest { QueryId = "c", Kind = ContainerQueryKind.Summary, ContainerId = Id }, Send, TestContext.Current.CancellationToken);

        Assert.Equal("invalid_container", sent[0].ContainerQueryResponse.FailureCode);
        Assert.Equal(QueryResultState.NotSupported, sent[1].ContainerQueryResponse.State);
        Assert.Equal(QueryResultState.NotFound, sent[2].ContainerQueryResponse.State);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class NullEngine : IContainerEngine
    {
        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("1", "1.47"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EngineContainer>>([]);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(Dokpod.Domain.Commands.AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerMutationResult(false, "x"));
    }
}
