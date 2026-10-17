using System.Text.Json;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Containers;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Containers;

/// <summary>Spec 013 T012/T018: autorização, política, capability, validação por engine e auditoria redigida.</summary>
public sealed class ContainerCreationServiceTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private const string Canary = "canario-013-api";

    private static ContainerCreationRequest Request(string extra = "") => JsonSerializer.Deserialize<ContainerCreationRequest>(
        "{\"image\":\"registry.empresa.com/pedidos/api:3.14.2\",\"name\":\"api-pedidos\",\"ports\":[{\"hostPort\":8081,\"containerPort\":80,\"protocol\":\"tcp\"}]," +
        "\"environment\":[{\"name\":\"DATABASE_URL\",\"value\":\"" + Canary + "\",\"sensitive\":true}],\"restartPolicy\":{\"name\":\"unless-stopped\"}" + extra + "}",
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private sealed class Harness(
        AuthorizationDecisionOutcome outcome = AuthorizationDecisionOutcome.Allowed,
        EnvironmentStage? stage = null,
        ImagePullPolicy? policy = null,
        bool connected = true,
        string[]? capabilities = null,
        string engine = "podman")
    {
        public Decider Authorization { get; } = new(outcome);
        public Store Store { get; } = new(AgentCommandEnqueueResult.Created);

        public Task<ContainerCreationResult> CreateAsync(ContainerCreationRequest request) =>
            new ContainerCreationService(
                    Authorization,
                    new Registrations(stage),
                    new Sessions(connected ? new AgentSession(EnvironmentId, Guid.NewGuid(), 9) : null),
                    new AgentCommandQueueService(Store, new Delivery(), TimeProvider.System),
                    new FakeHostStore(capabilities ?? ["Inventory", "ContainerCreate"], EnvironmentId, engine),
                    policy ?? new ImagePullPolicy(),
                    new AgentQueryDispatcher(TimeProvider.System),
                    TimeProvider.System)
                .CreateAsync(EnvironmentId, Guid.NewGuid(), request, AuthenticatedActor.FromSubject("u-1"), "token", Guid.NewGuid(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Create_EnqueuesHostCommandAndAuditsTheRedactedEquivalentCommand()
    {
        var harness = new Harness();

        var result = await harness.CreateAsync(Request());

        Assert.Equal(ContainerCreationOutcome.Accepted, result.Outcome);
        Assert.Equal(EnvironmentResourceScopes.CreateContainer, harness.Authorization.Scope);
        var command = harness.Store.Command!.Command;
        Assert.Equal(AgentCommandKind.CreateContainer, command.Kind);
        Assert.Equal(string.Empty, command.ContainerId);
        Assert.InRange(command.DeadlineUtc - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(16), TimeSpan.FromMinutes(17));
        var audit = harness.Store.AuditEvent!;
        Assert.Equal("container.create.requested", audit.Action);
        Assert.Equal("api-pedidos", audit.SubjectId);
        Assert.Contains("podman run -d --name api-pedidos", audit.Details!["command"]);
        Assert.Contains("DATABASE_URL=***", audit.Details["command"]);
        Assert.DoesNotContain(Canary, JsonSerializer.Serialize(audit.Details));
        Assert.DoesNotContain(Canary, result.EquivalentCommand);
    }

    [Fact]
    public async Task Create_MasksSecretsOfTheHealthCheckInTheEquivalentCommandAndAudit()
    {
        // Spec 023: o comando de saúde pode carregar segredo; resposta e auditoria saem com o padrão mascarado.
        var harness = new Harness();

        var result = await harness.CreateAsync(Request(
            ",\"healthcheck\":{\"test\":[\"CMD-SHELL\",\"curl -H 'Authorization: Bearer canario-saude-023' http://127.0.0.1/\"]," +
            "\"intervalSeconds\":30,\"timeoutSeconds\":5,\"retries\":3,\"startPeriodSeconds\":0,\"onFailure\":\"none\"}"));

        Assert.Equal(ContainerCreationOutcome.Accepted, result.Outcome);
        Assert.DoesNotContain("canario-saude-023", result.EquivalentCommand);
        Assert.Contains("--health-cmd", result.EquivalentCommand);
        Assert.DoesNotContain("canario-saude-023", JsonSerializer.Serialize(harness.Store.AuditEvent!.Details));
        // O agente continua recebendo o comando real.
        var spec = Assert.IsType<CreateContainerParameters>(harness.Store.Command!.Command.Parameters).Spec;
        Assert.Contains("canario-saude-023", spec.HealthCheck!.Test[1]);
    }

    [Fact]
    public async Task Create_InvalidSpecReturnsFieldErrorsBeforeAuthorizing()
    {
        var harness = new Harness();

        var result = await harness.CreateAsync(Request(",\"cpuLimitCores\":100,\"network\":\"host\""));

        Assert.Equal(ContainerCreationOutcome.InvalidSpec, result.Outcome);
        Assert.Contains(new FieldError("cpuLimitCores", "out_of_range"), result.Errors);
        Assert.Contains(new FieldError("network", "invalid_network"), result.Errors);
        Assert.Null(harness.Authorization.Scope);
        Assert.Null(harness.Store.Command);
    }

    [Fact]
    public async Task Create_NegativePaths()
    {
        var policy = new ImagePullPolicy { AllowedRegistriesByStage = new(StringComparer.OrdinalIgnoreCase) { ["production"] = ["quay.io"] } };

        Assert.Equal(ContainerCreationOutcome.AuthorizationDenied, (await new Harness(AuthorizationDecisionOutcome.Denied).CreateAsync(Request())).Outcome);
        Assert.Equal(ContainerCreationOutcome.RegistryNotAllowed, (await new Harness(stage: EnvironmentStage.Production, policy: policy).CreateAsync(Request())).Outcome);
        Assert.Equal(ContainerCreationOutcome.AgentOffline, (await new Harness(connected: false).CreateAsync(Request())).Outcome);
        Assert.Equal(ContainerCreationOutcome.NotSupported, (await new Harness(capabilities: ["Inventory"]).CreateAsync(Request())).Outcome);
        var health = ",\"healthcheck\":{\"test\":[\"CMD\",\"true\"],\"intervalSeconds\":15,\"timeoutSeconds\":5,\"retries\":3,\"startPeriodSeconds\":0,\"onFailure\":\"restart\"}";
        var docker = await new Harness(engine: "docker").CreateAsync(Request(health));
        Assert.Equal(ContainerCreationOutcome.InvalidSpec, docker.Outcome);
        Assert.Contains(new FieldError("healthcheck.onFailure", "unsupported_option"), docker.Errors);
        Assert.Equal(ContainerCreationOutcome.Accepted, (await new Harness().CreateAsync(Request(health))).Outcome);
        var past = await new Harness().CreateAsync(Request(",\"deadlineUtc\":\"2020-01-01T00:00:00Z\""));
        Assert.Contains(new FieldError("deadlineUtc", "out_of_range"), past.Errors);
    }
}
