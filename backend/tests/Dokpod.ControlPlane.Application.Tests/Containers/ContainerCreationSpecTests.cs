using System.Text.Json;
using Dokpod.ControlPlane.Application.Containers;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Containers;

/// <summary>Spec 013 T005/T006: allowlist, limites, regras cruzadas, hash canônico e paridade do comando equivalente.</summary>
public sealed class ContainerCreationSpecTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ContainerCreationRequest Parse(string json) => JsonSerializer.Deserialize<ContainerCreationRequest>(json, Json)!;

    private static ContainerCreationSpec Valid(Func<string, string>? edit = null)
    {
        var json = """{"image":"nginx:1.27","name":"web-01","ports":[{"hostPort":8081,"containerPort":80,"protocol":"tcp"}],"environment":[{"name":"A","value":"1","sensitive":false}],"volumes":[],"restartPolicy":{"name":"no"},"autoRemove":false}""";
        var (spec, errors) = Parse(edit?.Invoke(json) ?? json).ToSpec();
        Assert.Empty(errors);
        return spec!;
    }

    public static TheoryData<string> ForbiddenFields => new()
    {
        "\"privileged\":true",
        "\"capAdd\":[\"SYS_ADMIN\"]",
        "\"devices\":[\"/dev/sda\"]",
        "\"pidMode\":\"host\"",
        "\"ipcMode\":\"host\"",
        "\"networkMode\":\"host\"",
        "\"securityOpt\":[\"seccomp=unconfined\"]",
        "\"sysctls\":{\"net.ipv4.ip_forward\":\"1\"}",
        "\"usernsMode\":\"host\"",
        "\"binds\":[\"/var/run/docker.sock:/var/run/docker.sock\"]",
        "\"user\":\"root\"",
    };

    [Theory]
    [MemberData(nameof(ForbiddenFields))]
    public void Request_RejectsAnyFieldOutsideTheAllowlist(string field)
    {
        var json = "{\"image\":\"nginx\",\"name\":\"x\"," + field + "}";
        Assert.Throws<JsonException>(() => Parse(json));
    }

    [Fact]
    public void Request_RejectsUnknownNestedFields()
    {
        Assert.Throws<JsonException>(() => Parse("""{"image":"nginx","name":"x","ports":[{"hostPort":1,"containerPort":1,"protocol":"tcp","mode":"host"}]}"""));
        Assert.Throws<JsonException>(() => Parse("""{"image":"nginx","name":"x","volumes":[{"type":"volume","source":"a","target":"/a","propagation":"shared"}]}"""));
    }

    [Theory]
    [InlineData("\"name\":\"web-01\"", "\"name\":\"-web\"", "name", "invalid_name")]
    [InlineData("\"name\":\"web-01\"", "\"name\":\"a234567890123456789012345678901234567890123456789012345678901234\"", "name", "invalid_name")]
    [InlineData("\"hostPort\":8081", "\"hostPort\":70000", "ports[0].hostPort", "out_of_range")]
    [InlineData("\"protocol\":\"tcp\"", "\"protocol\":\"sctp\"", "ports[0].protocol", "invalid_protocol")]
    [InlineData("\"name\":\"A\"", "\"name\":\"1A\"", "environment[0].name", "invalid_name")]
    [InlineData("\"volumes\":[]", "\"volumes\":[{\"type\":\"volume\",\"source\":\"v\",\"target\":\"/\"}]", "volumes[0].target", "invalid_path")]
    [InlineData("\"volumes\":[]", "\"volumes\":[{\"type\":\"volume\",\"source\":\"v\",\"target\":\"/a/../etc\"}]", "volumes[0].target", "invalid_path")]
    [InlineData("\"volumes\":[]", "\"volumes\":[{\"type\":\"bind\",\"source\":\"relativo\",\"target\":\"/a\"}]", "volumes[0].source", "invalid_path")]
    [InlineData("\"autoRemove\":false", "\"autoRemove\":false,\"network\":\"host\"", "network", "invalid_network")]
    [InlineData("\"autoRemove\":false", "\"autoRemove\":false,\"cpuLimitCores\":65", "cpuLimitCores", "out_of_range")]
    [InlineData("\"autoRemove\":false", "\"autoRemove\":false,\"memoryLimitBytes\":1024", "memoryLimitBytes", "out_of_range")]
    [InlineData("\"restartPolicy\":{\"name\":\"no\"},\"autoRemove\":false", "\"restartPolicy\":{\"name\":\"always\"},\"autoRemove\":true", "autoRemove", "incompatible_with_restart")]
    [InlineData("\"restartPolicy\":{\"name\":\"no\"}", "\"restartPolicy\":{\"name\":\"on-failure\",\"maximumRetryCount\":101}", "restartPolicy.maximumRetryCount", "out_of_range")]
    [InlineData("\"value\":\"1\"", "\"value\":\"1\",\"secret\":{\"kind\":\"file\",\"name\":\"s\"}", "environment[0]", "value_or_secret_required")]
    [InlineData("\"value\":\"1\"", "\"secret\":{\"kind\":\"file\",\"name\":\"../etc/passwd\"}", "environment[0].secret.name", "invalid_name")]
    public void Validate_ReportsFieldErrors(string from, string to, string field, string code)
    {
        var spec = Valid(json => json.Replace(from, to));

        Assert.Contains(new FieldError(field, code), spec.Validate());
    }

    [Fact]
    public void Validate_EnforcesFixedLimits()
    {
        var many = Valid() with
        {
            Ports = Enumerable.Range(1, 33).Select(i => new PortSpec(null, 1000 + i, 80, "tcp")).ToArray(),
            Environment = Enumerable.Range(1, 101).Select(i => new EnvironmentSpec($"V{i}", "x", null, false)).ToArray(),
            Volumes = Enumerable.Range(1, 11).Select(i => new VolumeSpec(VolumeKind.Volume, $"v{i}", $"/d{i}", false)).ToArray(),
        };
        var errors = many.Validate();

        Assert.Contains(new FieldError("ports", "too_many"), errors);
        Assert.Contains(new FieldError("environment", "too_many"), errors);
        Assert.Contains(new FieldError("volumes", "too_many"), errors);
        Assert.Contains(new FieldError("environment[0].value", "too_long"),
            (Valid() with { Environment = [new EnvironmentSpec("BIG", new string('x', 32 * 1024 + 1), null, false)] }).Validate());
    }

    [Fact]
    public void Validate_HealthRestartOnlyOnPodman()
    {
        var spec = Valid() with { HealthCheck = new HealthCheckSpec(["CMD", "true"], 15, 5, 3, 0, HealthCheckOnFailure.Restart) };

        Assert.Empty(spec.Validate("podman"));
        Assert.Contains(new FieldError("healthcheck.onFailure", "unsupported_option"), spec.Validate("docker"));
        Assert.Contains(new FieldError("healthcheck.test", "invalid_test"),
            (spec with { HealthCheck = new HealthCheckSpec(["NONE"], 15, 5, 3, 0, HealthCheckOnFailure.None) }).Validate());
    }

    [Fact]
    public void Canonical_HashesValuesAndNeverContainsThem()
    {
        var spec = Valid(json => json.Replace("\"value\":\"1\"", "\"value\":\"canario-013-valor\""));
        var other = Valid(json => json.Replace("\"value\":\"1\"", "\"value\":\"outro\""));

        Assert.DoesNotContain("canario-013-valor", spec.Canonical());
        Assert.NotEqual(spec.Canonical(), other.Canonical());
        var hash = AgentCommandKinds.PayloadHash(AgentCommandKind.CreateContainer, "", "", new CreateContainerParameters(spec));
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void WithoutValues_DropsLiteralsAndWithValuesRestoresThem()
    {
        var spec = Valid(json => json.Replace("\"value\":\"1\"", "\"value\":\"canario\""));
        var stripped = spec.WithoutValues();

        Assert.Null(stripped.Environment[0].Value);
        Assert.False(stripped.ValuesAvailable);
        Assert.Empty(stripped.Validate());
        var restored = stripped.WithValues(spec.LiteralValues());
        Assert.Equal("canario", restored.Environment[0].Value);
        Assert.Equal(spec.Canonical(), restored.Canonical());
    }

    [Fact]
    public void HealthCheck_IsStoredOnlyAmongProtectedValuesAndUnavailableWithoutThem()
    {
        // Spec 023: o comando de saúde segue o caminho dos valores das variáveis (cifrado, apagado ao terminar o comando).
        var spec = Valid(json => json.Replace("\"environment\":[{\"name\":\"A\",\"value\":\"1\",\"sensitive\":false}]", "\"environment\":[]")
            .Replace("\"autoRemove\":false", "\"autoRemove\":false,\"healthcheck\":{\"test\":[\"CMD\",\"curl\",\"-u\",\"app:canario-saude\"],\"intervalSeconds\":30,\"timeoutSeconds\":5,\"retries\":3,\"startPeriodSeconds\":0,\"onFailure\":\"none\"}"));
        var stripped = spec.WithoutValues();

        Assert.Equal(["CMD", "***"], stripped.HealthCheck!.Test);
        Assert.False(stripped.ValuesAvailable);
        Assert.Empty(stripped.Validate());
        var restored = stripped.WithValues(spec.LiteralValues());
        Assert.True(restored.ValuesAvailable);
        Assert.Equal(spec.HealthCheck!.Test, restored.HealthCheck!.Test);
        Assert.Equal(spec.Canonical(), restored.Canonical());
        Assert.False(stripped.WithValues(new Dictionary<string, string>()).ValuesAvailable);
    }

    [Fact]
    public void EquivalentCommand_MatchesSharedFixtures()
    {
        var path = FindFixture();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var count = 0;
        foreach (var fixture in document.RootElement.EnumerateArray())
        {
            var (spec, errors) = JsonSerializer.Deserialize<ContainerCreationRequest>(fixture.GetProperty("request").GetRawText(), Json)!.ToSpec();
            Assert.Empty(errors);
            var engine = fixture.GetProperty("engine").GetString()!;
            Assert.Empty(spec!.Validate(engine));
            Assert.Equal(fixture.GetProperty("expected").GetString(), EquivalentCommandRenderer.Render(spec, engine));
            Assert.DoesNotContain("production", EquivalentCommandRenderer.Render(spec, engine).Replace("--name", ""), StringComparison.Ordinal);
            count++;
        }

        Assert.True(count >= 4);
    }

    private static string FindFixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "frontend", "web", "src", "testing", "equivalent-command.fixtures.json");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("equivalent-command.fixtures.json");
    }
}
