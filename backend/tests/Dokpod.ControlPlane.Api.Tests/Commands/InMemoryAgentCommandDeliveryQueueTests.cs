using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.Domain.Commands;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Commands;

/// <summary>
/// Bug Fixing fencing-reconexao-status-unknown: na reconexão, a sessão cercada e a nova leem a fila do mesmo ambiente
/// ao mesmo tempo; uma leitura não pode cancelar a outra.
/// </summary>
public sealed class InMemoryAgentCommandDeliveryQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConcurrentReadersOfTheSameEnvironment_DoNotCancelEachOther_AndACommandGoesToExactlyOne()
    {
        var queue = new InMemoryAgentCommandDeliveryQueue();
        var environmentId = Guid.NewGuid();
        var first = queue.DequeueAsync(environmentId, Ct).AsTask();
        var second = queue.DequeueAsync(environmentId, Ct).AsTask();

        await Task.Delay(100, Ct);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        var command = Command(environmentId);
        await queue.EnqueueAsync(command, Ct);
        var delivered = await Task.WhenAny(first, second).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Equal(command.Command.CommandId, (await delivered).Command.CommandId);
        await Task.Delay(100, Ct);
        Assert.False((delivered == first ? second : first).IsCompleted);
    }

    private static PersistedAgentCommand Command(Guid environmentId) => new(
        new AgentCommand(
            environmentId,
            Guid.NewGuid(),
            AgentCommandKind.RestartContainer,
            new string('A', 64),
            "revision-01",
            new string('B', 64),
            DateTimeOffset.UtcNow.AddMinutes(1),
            1),
        ControlPlaneCommandState.Pending,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
