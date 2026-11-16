using System.Text.Json;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;

namespace Dokpod.ControlPlane.Api.Admin;

/// <summary>Códigos de saída do modo administrativo (specs/033-instalador-guiado/contracts/admin-commands.md).</summary>
public static class AdminExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int Usage = 2;
    public const int MigrationDivergence = 6;
    public const int IdentityConflict = 8;
}

/// <summary>
/// Onde o modo administrativo lê a conexão e a sobreposição do catálogo SQL. Na imagem, <c>/run/secrets</c> e
/// <c>/run/sql-override</c>, montados pelo instalador; os testes passam diretórios temporários.
/// </summary>
public sealed record AdminContext(string SecretsDirectory, string SqlOverrideDirectory, TextWriter Out, TextWriter Error)
{
    public const string ConnectionFile = "ConnectionStrings__ControlPlane";

    /// <summary>Edição desta imagem; os testes trocam para exercitar os dois lados de <c>licensing import</c>.</summary>
    public string Edition { get; init; } = ProductEdition.Current;

    public static AdminContext ForImage() => new("/run/secrets", "/run/sql-override", Console.Out, Console.Error);
}

/// <summary>
/// Spec 033 T010 (FR-009, D-003, D-004): <c>dotnet Dokpod.ControlPlane.Api.dll admin &lt;grupo&gt; &lt;comando&gt;</c>. Monta só o
/// <see cref="ControlPlaneDbContext"/> e os serviços do comando, sem Kestrel, gRPC, SignalR nem hosted services, e termina
/// ao fim do comando. A conexão vem só do arquivo montado; não há opção de conexão na linha de comando. Saída JSON em
/// stdout e mensagens sem segredos em stderr.
/// </summary>
public static class AdminCommandHost
{
    public const string Verb = "admin";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args, AdminContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);
        if (args.Length < 2 || args.Contains("--help", StringComparer.Ordinal))
        {
            await context.Out.WriteLineAsync(Help).ConfigureAwait(false);
            return args.Contains("--help", StringComparer.Ordinal) ? AdminExitCodes.Success : AdminExitCodes.Usage;
        }

        AdminArguments arguments;
        try
        {
            arguments = AdminArguments.Parse(args[2..]);
        }
        catch (AdminUsageException error)
        {
            return await UsageAsync(context, error.Message).ConfigureAwait(false);
        }

        var connectionPath = Path.Combine(context.SecretsDirectory, AdminContext.ConnectionFile);
        if (!File.Exists(connectionPath))
        {
            return await UsageAsync(context, $"A conexão do banco não está montada em {connectionPath}.").ConfigureAwait(false);
        }

        var connection = (await File.ReadAllTextAsync(connectionPath, cancellationToken).ConfigureAwait(false)).Trim();
        SqlCatalogOverride? sqlOverride = null;
        try
        {
            if (Directory.Exists(context.SqlOverrideDirectory) && Directory.EnumerateFileSystemEntries(context.SqlOverrideDirectory).Any())
            {
                sqlOverride = SqlCatalog.UseOverride(context.SqlOverrideDirectory);
            }

            await using var database = CreateContext(connection);
            return (args[0], args[1]) switch
            {
                ("migrations", _) => await MigrationsCommands.RunAsync(args[1], arguments, database, context, cancellationToken).ConfigureAwait(false),
                ("environments", _) or ("agent-identities", _) =>
                    await BootstrapCommands.RunAsync(args[0], args[1], arguments, database, context, cancellationToken).ConfigureAwait(false),
                ("licensing", _) => await ProductEdition.RunLicensingAdminAsync(args[1], arguments, database, context, cancellationToken).ConfigureAwait(false),
                _ => await UsageAsync(context, $"Grupo desconhecido: {args[0]}.").ConfigureAwait(false),
            };
        }
        catch (AdminUsageException error)
        {
            return await UsageAsync(context, error.Message).ConfigureAwait(false);
        }
        catch (SqlCatalogOverrideException error)
        {
            await context.Error.WriteLineAsync($"Sobreposição do catálogo SQL recusada: {error.Message}").ConfigureAwait(false);
            return AdminExitCodes.Usage;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Só o tipo e a mensagem do erro: a conexão nunca entra em mensagem de exceção do Npgsql.
            await context.Error.WriteLineAsync($"Falha no comando {args[0]} {args[1]}: {error.GetType().Name}: {error.Message}").ConfigureAwait(false);
            return AdminExitCodes.Error;
        }
        finally
        {
            sqlOverride?.Dispose();
        }
    }

    internal static async Task<int> WriteAsync(AdminContext context, object result, int exitCode = AdminExitCodes.Success)
    {
        await context.Out.WriteLineAsync(JsonSerializer.Serialize(result, Json)).ConfigureAwait(false);
        return exitCode;
    }

    internal static async Task<int> UsageAsync(AdminContext context, string message)
    {
        await context.Error.WriteLineAsync($"{message} Consulte 'admin --help'.").ConfigureAwait(false);
        return AdminExitCodes.Usage;
    }

    // Mesma sessão do runtime (UTC, por ControlPlaneConnectionString) e o schema do Dokpod quando a conexão não o define.
    private static ControlPlaneDbContext CreateContext(string connection)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (string.IsNullOrWhiteSpace(builder.SearchPath))
        {
            builder.SearchPath = "dokpod";
        }

        return ControlPlaneDbContextFactory.Create(builder.ConnectionString);
    }

    private const string Help = """
        Modo administrativo do Dokpod (spec 033). Conexão: /run/secrets/ConnectionStrings__ControlPlane.

          admin migrations status
          admin migrations plan
          admin migrations script --out <arquivo>
          admin migrations apply --confirm <planHash>
          admin migrations fingerprint
          admin migrations verify [--script <arquivo>] [--accepted-fingerprints <arquivo>]
          admin environments register --id <uuid> --name <nome> --host <host> --stage <production|staging|development|none> --actor <operador> --reason <texto>
          admin agent-identities register --environment <uuid> --fingerprint <sha256> --actor <operador> --reason <texto>
          admin agent-identities revoke --environment <uuid> --actor <operador> --reason <texto>
          admin licensing import --file <licença> --slot <prd|hml> --actor <operador> --reason <texto>   (só Business)

        Saída: JSON em stdout. Códigos: 0 sucesso, 1 erro, 2 uso inválido, 6 divergência de migrations, 8 conflito de identidade.
        """;
}

/// <summary>Opções <c>--nome valor</c>; repetir uma opção ou deixá-la sem valor é uso inválido.</summary>
internal sealed class AdminArguments
{
    private readonly Dictionary<string, string> values;

    private AdminArguments(Dictionary<string, string> values) => this.values = values;

    public static AdminArguments Parse(IReadOnlyList<string> tokens)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length < 3)
            {
                throw new AdminUsageException($"Argumento inesperado: {token}.");
            }

            if (index + 1 >= tokens.Count || tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new AdminUsageException($"A opção {token} precisa de um valor.");
            }

            if (!values.TryAdd(token[2..], tokens[++index]))
            {
                throw new AdminUsageException($"A opção {token} foi repetida.");
            }
        }

        return new AdminArguments(values);
    }

    public string Required(string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new AdminUsageException($"Falta a opção --{name}.");

    public string? Optional(string name) => values.GetValueOrDefault(name);

    public Guid RequiredGuid(string name) =>
        Guid.TryParse(Required(name), out var value) && value != Guid.Empty
            ? value
            : throw new AdminUsageException($"A opção --{name} precisa ser um UUID.");

    /// <summary>Recusa opções que o comando não conhece (<c>--output json</c> é sempre aceita).</summary>
    public void OnlyAllow(params string[] names)
    {
        var unknown = values.Keys.Where(key => key != "output" && !names.Contains(key, StringComparer.Ordinal)).ToArray();
        if (unknown.Length > 0)
        {
            throw new AdminUsageException($"Opção desconhecida: --{unknown[0]}.");
        }

        if (values.TryGetValue("output", out var output) && output != "json")
        {
            throw new AdminUsageException("Só há saída --output json.");
        }
    }
}

internal sealed class AdminUsageException(string message) : Exception(message);
