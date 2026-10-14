using Npgsql;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>
/// Connection string do plano de controle com a sessão fixada em UTC. Os limites das partições mensais e a retenção
/// são calculados em UTC; a migration 202609220002 converte datas com o fuso da sessão e, num servidor fora de UTC,
/// criava partições sobrepostas. Migrations publicadas são imutáveis, então a correção fica no executor.
/// </summary>
public static class ControlPlaneConnectionString
{
    public static string Normalize(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Timezone = "UTC" };
        return builder.ConnectionString;
    }
}
