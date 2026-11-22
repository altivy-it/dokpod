using System.Reflection;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Terminal;
using Xunit;

namespace Dokpod.Architecture.Tests;

/// <summary>
/// Spec 026 T009 (D-001): toda ação que muda o host passa pela janela de mudança. A fila já exige a liberação; este
/// teste cobre o que a compilação não cobre: quem enfileira também recebe o guard, o terminal (fora da fila) também, e
/// só o guard emite a liberação.
/// </summary>
public sealed class ChangeWindowGuardTests
{
    private static readonly Assembly Application = typeof(AgentCommandQueueService).Assembly;

    [Fact]
    public void EveryTypeThatEnqueuesCommands_AlsoDependsOnTheGuard()
    {
        var enqueuers = Application.GetTypes().Where(DependsOn<AgentCommandQueueService>).ToArray();

        Assert.NotEmpty(enqueuers);
        Assert.Empty(MissingGuard(Application.GetTypes()));
    }

    [Fact]
    public void Rule_RejectsATypeThatEnqueuesWithoutTheGuard()
    {
        Assert.Equal([typeof(EnqueuerWithoutGuard)], MissingGuard([typeof(EnqueuerWithoutGuard), typeof(EnqueuerWithGuard)]));
    }

    [Fact]
    public void Terminal_DependsOnTheGuard()
    {
        Assert.True(DependsOn<ChangeWindowGuard>(typeof(TerminalSessionService)));
    }

    /// <summary>
    /// SC-003 (FR-003): só os casos de uso que mudam o host dependem do guard; leituras, logs, inspeção, alertas e
    /// auditoria nunca passam por ele. E o guard não depende do gate da licença (FR-007).
    /// </summary>
    [Fact]
    public void OnlyTheMutatingUseCases_DependOnTheGuard_AndTheGuardIgnoresTheLicense()
    {
        var dependents = Application.GetTypes().Where(DependsOn<ChangeWindowGuard>).Select(type => type.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            ["ContainerCommandService", "ContainerCreationService", "ImagePullService", "NetworkRemovalService", "PodActionService", "TerminalSessionService", "VolumeRemovalService"],
            dependents);
        Assert.DoesNotContain(
            typeof(ChangeWindowGuard).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(Dokpod.ControlPlane.Application.Licensing.IBusinessCapabilityGate));
    }

    [Fact]
    public void OnlyTheGuard_IssuesClearances()
    {
        var clearance = typeof(ChangeWindowGuard.Clearance);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Só construtores privados: nem a fila nem os serviços, no mesmo assembly, conseguem criar uma liberação.
        Assert.All(clearance.GetConstructors(all).Where(constructor => !constructor.IsStatic), constructor => Assert.True(constructor.IsPrivate));
        Assert.DoesNotContain(clearance.GetMethods(all), method => method.IsStatic && method.ReturnType == clearance);

        // Nenhum outro tipo expõe um método estático que devolve a liberação.
        var factories = Application.GetTypes()
            .Where(type => type != typeof(ChangeWindowGuard) && type != clearance)
            .SelectMany(type => type.GetMethods(all))
            .Where(method => method.IsStatic && method.ReturnType == clearance)
            .Select(method => $"{method.DeclaringType}.{method.Name}")
            .ToArray();
        Assert.Empty(factories);
    }

    private static Type[] MissingGuard(IEnumerable<Type> types) =>
        types.Where(DependsOn<AgentCommandQueueService>).Where(type => !DependsOn<ChangeWindowGuard>(type)).ToArray();

    private static bool DependsOn<T>(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(T));

    private sealed class EnqueuerWithoutGuard(AgentCommandQueueService queue)
    {
        public AgentCommandQueueService Queue => queue;
    }

    private sealed class EnqueuerWithGuard(AgentCommandQueueService queue, ChangeWindowGuard guard)
    {
        public AgentCommandQueueService Queue => queue;

        public ChangeWindowGuard Guard => guard;
    }
}
