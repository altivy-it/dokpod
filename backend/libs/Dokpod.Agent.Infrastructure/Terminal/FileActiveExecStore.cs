using System.Text.Json;
using Dokpod.Agent.Application.Terminal;

namespace Dokpod.Agent.Infrastructure.Terminal;

/// <summary>`&lt;data&gt;/exec-sessions.json` com os execs ativos (spec 014 D-007); escrita atômica (arquivo temporário + rename).</summary>
public sealed class FileActiveExecStore(string dataDirectory) : IActiveExecStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string FilePath => Path.Combine(dataDirectory, "exec-sessions.json");

    public async Task AddAsync(ActiveExec exec, CancellationToken cancellationToken) =>
        await MutateAsync(list => list.Where(e => e.ExecSessionId != exec.ExecSessionId).Append(exec).ToList(), cancellationToken);

    public async Task RemoveAsync(string execSessionId, CancellationToken cancellationToken) =>
        await MutateAsync(list => list.Where(e => e.ExecSessionId != execSessionId).ToList(), cancellationToken);

    public async Task<IReadOnlyList<ActiveExec>> ListAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task MutateAsync(Func<List<ActiveExec>, List<ActiveExec>> change, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var next = change(await ReadAsync(cancellationToken));
            Directory.CreateDirectory(dataDirectory);
            var temporary = FilePath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(next), cancellationToken);
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<ActiveExec>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ActiveExec>>(await File.ReadAllTextAsync(FilePath, cancellationToken)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
