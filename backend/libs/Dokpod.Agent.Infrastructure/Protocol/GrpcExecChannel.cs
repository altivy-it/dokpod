using Dokpod.Agent.Application.Terminal;
using Dokpod.Agent.Contracts.V1;
using Grpc.Core;

namespace Dokpod.Agent.Infrastructure.Protocol;

/// <summary>Adapta a chamada `Exec` do gRPC ao canal usado pelo `ExecSessionManager` (escritas serializadas).</summary>
public sealed class GrpcExecChannel(AsyncDuplexStreamingCall<ExecFrame, ExecFrame> call) : IExecChannel
{
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private bool completed;

    public async Task SendAsync(ExecFrame frame, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!completed) await call.RequestStream.WriteAsync(frame, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public IAsyncEnumerable<ExecFrame> ReadAllAsync(CancellationToken cancellationToken) => call.ResponseStream.ReadAllAsync(cancellationToken);

    public async Task CompleteAsync()
    {
        await writeLock.WaitAsync();
        try
        {
            if (completed) return;
            completed = true;
            await call.RequestStream.CompleteAsync();
        }
        finally
        {
            writeLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        call.Dispose();
        return ValueTask.CompletedTask;
    }
}
