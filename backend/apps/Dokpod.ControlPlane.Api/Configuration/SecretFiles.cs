namespace Dokpod.ControlPlane.Api.Configuration;

/// <summary>
/// Spec 033 (FR-033, D-006): segredos entregues por arquivo em <c>/run/secrets</c> (Compose <c>secrets:</c>), um arquivo
/// por chave, com <c>__</c> como separador de seção (por exemplo <c>ConnectionStrings__ControlPlane</c>). O arquivo vence a
/// variável de ambiente, e o valor não aparece em <c>docker inspect</c>, no ambiente do processo nem no log. Sem o
/// diretório, nada muda: as variáveis de ambiente seguem valendo como antes.
/// </summary>
public static class SecretFiles
{
    public const string DefaultDirectory = "/run/secrets";

    /// <summary>Diretório alternativo, só para testes e laboratório; o valor é um caminho, nunca um segredo.</summary>
    public const string DirectoryVariable = "DOKPOD_SECRETS_DIRECTORY";

    public static string Directory =>
        Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } configured ? configured : DefaultDirectory;

    /// <summary>Acrescenta os arquivos do diretório como a fonte de configuração de maior precedência, se ele existir.</summary>
    public static void AddTo(IConfigurationBuilder configuration, string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var root = directory ?? Directory;
        if (System.IO.Directory.Exists(root))
        {
            configuration.AddKeyPerFile(root, optional: true, reloadOnChange: false);
        }
    }

    /// <summary>Valor de um segredo lido fora da configuração (como a senha do PFX): arquivo primeiro, depois a variável.</summary>
    public static string? Read(string name, string? directory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = Path.Combine(directory ?? Directory, name);
        return File.Exists(path) ? File.ReadAllText(path).TrimEnd('\r', '\n') : Environment.GetEnvironmentVariable(name);
    }
}
