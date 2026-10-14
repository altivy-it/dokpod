using System.Net;
using System.Text;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Engines;

public sealed class DockerEngineClientTests
{
    private const string ContainerId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task InspectAsync_NegotiatesMaximumSupportedApiVersion()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""
            {"Version":"28.0.1","ApiVersion":"1.51","MinAPIVersion":"1.24"}
            """));
        var client = CreateClient(handler);

        var descriptor = await client.InspectAsync(TestContext.Current.CancellationToken);

        Assert.Equal("28.0.1", descriptor.EngineVersion);
        Assert.Equal("1.47", descriptor.ApiVersion);
        Assert.Equal("/version", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidContainerIdBeforeRequest()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("HTTP must not be called."));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteAsync(
            AgentCommandKind.DeleteContainer,
            "../../containers/json",
            TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteNeverRemovesVolumesOrForcesRemoval()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/version"
            ? JsonResponse("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        var result = await client.ExecuteAsync(
            AgentCommandKind.DeleteContainer,
            ContainerId,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/v1.47/containers/{ContainerId}?v=0&force=0", request.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task InspectContainerAsync_ReturnsNullForMissingTarget()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/version"
            ? JsonResponse("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.InspectContainerAsync(ContainerId, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private const string PodmanVersion = """{"Version":"5.7.0","ApiVersion":"1.41","MinAPIVersion":"1.24","Components":[{"Name":"Podman Engine"}]}""";

    [Theory]
    [InlineData(AgentCommandKind.PauseContainer, "not_running")]
    [InlineData(AgentCommandKind.UnpauseContainer, "not_paused")]
    public async Task ExecuteAsync_MapsPodmanStateImproperLikeDockerConflict(AgentCommandKind kind, string expected)
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/version"
            ? JsonResponse(PodmanVersion)
            : new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"cause":"container state improper","message":"x","response":500}""", Encoding.UTF8, "application/json"),
            });

        var result = await CreateClient(handler).ExecuteAsync(kind, ContainerId, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_PodmanGenericServerErrorStaysOperationFailed()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/version"
            ? JsonResponse(PodmanVersion)
            : new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("""{"cause":"io error"}""") });

        var result = await CreateClient(handler).ExecuteAsync(AgentCommandKind.PauseContainer, ContainerId, TestContext.Current.CancellationToken);

        Assert.Equal("operation_failed", result.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_PodmanUnpausesBeforeStoppingPausedContainer()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => JsonResponse(PodmanVersion),
            var path when path.EndsWith("/json", StringComparison.Ordinal) => JsonResponse($$$"""
                {"Id":"{{{ContainerId}}}","Name":"/w","Created":"2026-10-13T00:00:00Z","Config":{"Image":"nginx"},"State":{"Status":"paused"}}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
        });

        var result = await CreateClient(handler).ExecuteAsync(AgentCommandKind.StopContainer, ContainerId, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var mutations = handler.Requests.Where(r => r.Method == HttpMethod.Post).Select(r => r.RequestUri!.AbsolutePath).ToArray();
        Assert.Equal([$"/v1.41/containers/{ContainerId}/unpause", $"/v1.41/containers/{ContainerId}/stop"], mutations);
    }

    [Fact]
    public async Task ExecuteAsync_DockerStopsPausedContainerDirectly()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath == "/version"
            ? JsonResponse("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(HttpStatusCode.NoContent));

        await CreateClient(handler).ExecuteAsync(AgentCommandKind.StopContainer, ContainerId, TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/stop", handler.Requests.Last().RequestUri!.AbsolutePath);
    }

    private static DockerEngineClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}