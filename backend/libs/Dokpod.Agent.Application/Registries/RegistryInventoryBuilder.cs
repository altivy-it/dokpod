using System.Security.Cryptography;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;

namespace Dokpod.Agent.Application.Registries;

/// <summary>
/// Configuração de registros do host (spec 020 D-002): registros do engine, espelhos (como registros próprios, com a
/// origem) e registros com credencial no agente, em uma mensagem de até 500 registros; revisão só avança na mudança.
/// </summary>
public sealed class RegistryInventoryBuilder(TimeProvider timeProvider)
{
    public const int MaxRegistries = 500;
    public const int MaxMirrors = 16;

    private string? fingerprint;
    private ulong revision;

    public ulong Revision => Interlocked.Read(ref revision);

    /// <summary>Mensagem com a configuração, ou nulo quando nada mudou (e `force` é falso).</summary>
    public RegistryInventory? Build(EngineRegistryConfig config, bool force)
    {
        var states = new SortedDictionary<string, RegistryState>(StringComparer.Ordinal);
        RegistryState State(string name) => states.TryGetValue(name, out var state) ? state : states[name] = new RegistryState { Registry = name };
        foreach (var registry in config.Registries)
        {
            var state = State(registry.Registry);
            state.Insecure |= registry.Insecure;
            state.Blocked |= registry.Blocked;
            state.Search |= registry.Search;
            foreach (var mirror in registry.Mirrors.Take(MaxMirrors))
            {
                if (!state.Mirrors.Contains(mirror)) state.Mirrors.Add(mirror);
                var mirrorState = State(mirror);
                if (mirrorState.MirrorOf.Length == 0 && mirror != registry.Registry) mirrorState.MirrorOf = registry.Registry;
            }
        }

        foreach (var name in config.CredentialRegistries) State(name).Credential = true;
        var message = new RegistryInventory { ConfigAvailable = config.ConfigAvailable };
        message.Registries.AddRange(states.Values.Take(MaxRegistries));
        var current = Convert.ToHexString(SHA256.HashData(message.ToByteArray()));
        if (!force && current == fingerprint) return null;
        if (current != fingerprint)
        {
            var clock = (ulong)timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            Interlocked.Exchange(ref revision, Math.Max(Revision + 1, clock));
            fingerprint = current;
        }

        message.Revision = Revision;
        return message;
    }
}
