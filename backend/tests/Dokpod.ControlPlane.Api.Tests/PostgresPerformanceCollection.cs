using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>
/// Testes que medem tempo ou carregam muitas linhas em PostgreSQL real rodam sozinhos, depois das demais coleções:
/// em paralelo, uma carga (por exemplo, 1 milhão de eventos de auditoria) distorce a medição de outro teste.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresPerformanceCollection
{
    public const string Name = "postgres-performance";
}
