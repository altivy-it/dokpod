using Dokpod.Agent;
using Dokpod.Agent.Application.Commands;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Protocol;
using Dokpod.Agent.Infrastructure.Commands;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var options = AgentOptions.FromEnvironment();
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(service => service.ServiceName = "Dokpod Agent");
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ICommandJournal>(_ => new FileCommandJournal(options.DataDirectory));
builder.Services.AddSingleton(_ => OperatingSystem.IsWindows()
    ? DockerEngineClient.CreateNamedPipeClient(options.DockerSocketPath, options.EngineTimeout)
    : DockerEngineClient.CreateUnixSocketClient(options.DockerSocketPath, options.EngineTimeout));
builder.Services.AddSingleton(new Dokpod.Agent.Infrastructure.Engines.RegistryCredentialFile(options.RegistryAuthFile));
builder.Services.AddSingleton(new Dokpod.Agent.Infrastructure.Engines.Docker.EngineRawConnector(options.DockerSocketPath));
builder.Services.AddSingleton<IContainerEngine, DockerEngineClient>();
if (!OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<Dokpod.Agent.Application.Terminal.IActiveExecStore>(new Dokpod.Agent.Infrastructure.Terminal.FileActiveExecStore(options.DataDirectory));
    builder.Services.AddSingleton<Dokpod.Agent.Application.Terminal.ExecSessionManager>();
}
builder.Services.AddSingleton<Dokpod.Agent.Application.Metrics.IHostResourceProbe, Dokpod.Agent.Infrastructure.Metrics.ProcHostProbe>();
builder.Services.AddSingleton<AgentCommandGate>();
builder.Services.AddSingleton<Dokpod.Agent.Application.Containers.IHostSecretFiles>(new Dokpod.Agent.Infrastructure.Secrets.HostSecretFileStore(options.SecretsDirectory));
builder.Services.AddSingleton(new Dokpod.Agent.Application.Containers.ContainerCreationPolicy(options.BindMountAllowlist ?? [], options.DockerSocketPath));
builder.Services.AddSingleton<Dokpod.Agent.Application.Containers.ContainerCreator>();
builder.Services.AddSingleton<AgentCommandProcessor>();
builder.Services.AddSingleton<AgentCommandProtocolHandler>();
builder.Services.AddHostedService<AgentWorker>();
if (options.ControlPlaneEndpoint is not null && !options.RunOnce)
{
    builder.Services.AddHostedService<AgentControlWorker>();
}

await builder.Build().RunAsync();