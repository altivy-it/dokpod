using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task ControlPlaneContract_DocumentsEnvironmentEndpointsAndProblemResponses()
    {
        var contract = await File.ReadAllTextAsync(
            FindContractPath(),
            TestContext.Current.CancellationToken);

        Assert.Contains("openapi: 3.1.0", contract, StringComparison.Ordinal);
        Assert.Contains("/api/v1/environments/{environmentId}:", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getEnvironment", contract, StringComparison.Ordinal);
        Assert.Contains("/api/v1/environments/{environmentId}/containers:", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: listEnvironmentContainers", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: submitContainerCommand", contract, StringComparison.Ordinal);
        Assert.Contains("/api/v1/environments/{environmentId}/commands/{commandId}:", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getContainerCommand", contract, StringComparison.Ordinal);
        Assert.Contains("ContainerCommandStatus:", contract, StringComparison.Ordinal);
        Assert.Contains("name: Idempotency-Key", contract, StringComparison.Ordinal);
        Assert.Contains("maximum: 100", contract, StringComparison.Ordinal);
        Assert.Contains("/api/v1/environments:", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: listEnvironments", contract, StringComparison.Ordinal);
        Assert.Contains("EnvironmentPage:", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: registerEnvironment", contract, StringComparison.Ordinal);
        Assert.Contains("application/problem+json:", contract, StringComparison.Ordinal);
        Assert.Contains("'201':", contract, StringComparison.Ordinal);
        Assert.Contains("'400':", contract, StringComparison.Ordinal);
        Assert.Contains("'401':", contract, StringComparison.Ordinal);
        Assert.Contains("'403':", contract, StringComparison.Ordinal);
        Assert.Contains("'404':", contract, StringComparison.Ordinal);
        Assert.Contains("'409':", contract, StringComparison.Ordinal);
        Assert.Contains("'503':", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getOverview", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: listEvents", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: setEnvironmentStage", contract, StringComparison.Ordinal);
        Assert.Contains("HostAgentView:", contract, StringComparison.Ordinal);
        Assert.Contains("ContainerDisplayStatus:", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getStageMetrics", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: listImages", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: lookupImage", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: pullImage", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: createContainer", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getContainerCreateOptions", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getTerminalPolicy", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: renewTerminalSession", contract, StringComparison.Ordinal);
        Assert.Contains("ImageListPage:", contract, StringComparison.Ordinal);
        Assert.Contains("enum: [start, stop, restart, delete, pause, unpause, pull, create]", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: listHostMetrics", contract, StringComparison.Ordinal);
        Assert.Contains("operationId: getContainerMetrics", contract, StringComparison.Ordinal);
        Assert.Contains("ContainerResources:", contract, StringComparison.Ordinal);
        Assert.Contains("enum: [name, -name, displayStatus, environment, createdAt, -createdAt, cpu, -cpu, memory, -memory]", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("operationId: listHosts", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditEventEntity", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentRegistrationEntity", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("fencingToken:", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("payloadHash:", contract, StringComparison.Ordinal);
    }

    private static string FindContractPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "contracts",
                "openapi",
                "dokpod-control-plane.v1.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("OpenAPI contract was not found.");
    }
}
