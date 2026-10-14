using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>
/// Testes que usam `agent_commands` em PostgreSQL real rodam em série: `ClaimDispatchableAsync` expira comandos
/// vencidos de todos os ambientes, o que mudaria o estado esperado por outro teste em paralelo.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AgentCommandsPostgresCollection
{
    public const string Name = "agent-commands-postgres";
}
