using Dokpod.Agent.Application.Commands;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Protocol;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Images;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using DomainCommand = Dokpod.Domain.Commands.AgentCommand;

namespace Dokpod.Agent.Application.Tests.Engines;

/// <summary>Spec 012: mapeamento, execução e falhas do pull no agente (T007, T015).</summary>
public sealed class ImagePullTests
{
    private static readonly Guid Env = Guid.Parse("00000000-0000-4000-8000-000000000012");

    private static Dokpod.Agent.Contracts.V1.AgentCommand Message(string reference, string containerId = "", string revision = "") => new()
    {
        CommandId = Guid.NewGuid().ToString("D"),
        Kind = CommandKind.PullImage,
        ContainerId = containerId,
        ExpectedContainerRevision = revision,
        PayloadHash = ByteString.CopyFrom(new byte[32]),
        Deadline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(5)),
        PullImage = new Dokpod.Agent.Contracts.V1.PullImageParameters { Reference = reference },
    };

    private static readonly MessageMetadata Metadata = new() { FencingToken = 1 };

    [Fact]
    public void Mapper_AcceptsHostTargetedPullWithValidReference()
    {
        Assert.True(AgentCommandMapper.TryMap(Env, Metadata, Message("nginx:1.27"), out var command, out _));
        Assert.Equal(AgentCommandKind.PullImage, command!.Kind);
        Assert.Equal("docker.io/library/nginx:1.27", Assert.IsType<Dokpod.Domain.Commands.PullImageParameters>(command.Parameters).Reference.Canonical);
        Assert.Equal(string.Empty, command.ContainerId);
    }

    [Theory]
    [InlineData("user:pass@registry.empresa.com/app", "", "invalid_reference")]
    [InlineData("registry.empresa.com/app?x=1", "", "invalid_reference")]
    [InlineData("nginx", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "command_target_invalid")]
    public void Mapper_RevalidatesReferenceAndTarget(string reference, string containerId, string failure)
    {
        Assert.False(AgentCommandMapper.TryMap(Env, Metadata, Message(reference, containerId), out _, out var code));
        Assert.Equal(failure, code);
    }

    [Fact]
    public void Mapper_PullWithoutParametersIsInvalid()
    {
        var message = Message("nginx");
        message.ClearParameters();
        Assert.False(AgentCommandMapper.TryMap(Env, Metadata, message, out _, out var code));
        Assert.Equal("invalid_reference", code);
    }

    [Fact]
    public async Task Processor_RecordsDigestAndSizeAndRepeatsAfterLostResult()
    {
        var engine = new PullEngine(new ImagePullResult(true, null, "sha256:" + new string('c', 64), 1234));
        var journal = new MemoryJournal();
        var processor = new AgentCommandProcessor(new AgentCommandGate(journal, TimeProvider.System), journal, engine, TimeProvider.System);
        Assert.True(ImageReference.TryParse("nginx", out var reference));
        var command = new DomainCommand(Env, Guid.NewGuid(), AgentCommandKind.PullImage, string.Empty, string.Empty, "H", DateTimeOffset.UtcNow.AddMinutes(5), 1)
        {
            Parameters = new Dokpod.Domain.Commands.PullImageParameters(reference),
        };

        var result = await processor.ProcessAsync(command, 1, TestContext.Current.CancellationToken);
        Assert.Equal(CommandExecutionState.Succeeded, result.State);
        Assert.Equal(1234, result.Output!.ImageSizeBytes);

        journal.Results.Clear(); // resposta perdida após o aceite: o pull é refeito (idempotente)
        var again = await processor.ProcessAsync(command, 1, TestContext.Current.CancellationToken);
        Assert.Equal(CommandExecutionState.Succeeded, again.State);
        Assert.Equal(2, engine.Pulls);
    }

    [Fact]
    public async Task Processor_MapsEngineFailuresAndDeadline()
    {
        var journal = new MemoryJournal();
        Assert.True(ImageReference.TryParse("nginx", out var reference));
        var failing = new AgentCommandProcessor(new AgentCommandGate(journal, TimeProvider.System), journal, new PullEngine(new ImagePullResult(false, "image_not_found")), TimeProvider.System);
        var command = new DomainCommand(Env, Guid.NewGuid(), AgentCommandKind.PullImage, string.Empty, string.Empty, "H", DateTimeOffset.UtcNow.AddMinutes(5), 1)
        {
            Parameters = new Dokpod.Domain.Commands.PullImageParameters(reference),
        };
        Assert.Equal("image_not_found", (await failing.ProcessAsync(command, 1, TestContext.Current.CancellationToken)).FailureCode);

        var slow = new AgentCommandProcessor(new AgentCommandGate(journal, TimeProvider.System), journal, new PullEngine(null), TimeProvider.System);
        var shortDeadline = command with { CommandId = Guid.NewGuid(), DeadlineUtc = DateTimeOffset.UtcNow.AddMilliseconds(300) };
        Assert.Equal("deadline_exceeded", (await slow.ProcessAsync(shortDeadline, 1, TestContext.Current.CancellationToken)).FailureCode);
    }

    [Theory]
    [InlineData(500, "pull access denied for x, repository does not exist or may require 'docker login'", "image_not_found")]
    [InlineData(500, "manifest unknown", "image_not_found")]
    [InlineData(401, "unauthorized: authentication required", "registry_unauthorized")]
    [InlineData(500, "write /var/lib: no space left on device", "insufficient_storage")]
    [InlineData(500, "no matching manifest for linux/amd64 in the manifest list entries", "platform_mismatch")]
    [InlineData(500, "dial tcp: lookup registry.invalid: no such host", "registry_unreachable")]
    [InlineData(500, "algo inesperado", "image_pull_failed")]
    public void MapPullFailure_UsesContractCodes(int status, string message, string code)
    {
        Assert.Equal(code, DockerEngineClient.MapPullFailure(status, message));
    }

    [Fact]
    public void RegistryCredentials_AreReadOnlyForTheMatchingRegistry()
    {
        var file = Path.GetTempFileName();
        try
        {
            var auth = Convert.ToBase64String("bot:s3nh4"u8.ToArray());
            File.WriteAllText(file, "{\"auths\":{\"registry.empresa.com\":{\"auth\":\"" + auth + "\"}}}");
            var credentials = new RegistryCredentialFile(file);

            var header = credentials.HeaderFor("registry.empresa.com");
            Assert.NotNull(header);
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header.Replace('-', '+').Replace('_', '/').PadRight(header.Length + (4 - header.Length % 4) % 4, '=')));
            Assert.Contains("\"username\":\"bot\"", json);
            Assert.Null(credentials.HeaderFor("docker.io"));
            Assert.Null(new RegistryCredentialFile(null).HeaderFor("registry.empresa.com"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private sealed class PullEngine(ImagePullResult? result) : IContainerEngine
    {
        public int Pulls { get; private set; }

        public async Task<ImagePullResult> PullImageAsync(ImageReference reference, CancellationToken cancellationToken)
        {
            Pulls++;
            if (result is null) await Task.Delay(Timeout.Infinite, cancellationToken);
            return result!;
        }

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryJournal : ICommandJournal
    {
        private readonly Dictionary<Guid, JournaledCommand> commands = [];
        public Dictionary<Guid, JournaledCommandResult> Results { get; } = [];

        public Task<JournaledCommand?> AppendIfAbsentAsync(JournaledCommand command, CancellationToken cancellationToken)
        {
            if (commands.TryGetValue(command.CommandId, out var existing)) return Task.FromResult<JournaledCommand?>(existing);
            commands[command.CommandId] = command;
            return Task.FromResult<JournaledCommand?>(null);
        }

        public Task<JournaledCommandResult?> FindResultAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken) =>
            Task.FromResult(Results.GetValueOrDefault(commandId));

        public Task SaveResultAsync(JournaledCommandResult result, CancellationToken cancellationToken)
        {
            Results[result.CommandId] = result;
            return Task.CompletedTask;
        }
    }
}
