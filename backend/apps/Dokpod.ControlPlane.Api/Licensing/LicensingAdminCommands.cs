using Dokpod.ControlPlane.Api.Admin;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Dokpod.Licensing;

namespace Dokpod.ControlPlane.Api.Licensing;

/// <summary>
/// Spec 033 T015 (FR-031): <c>licensing import</c>, só na edição Business. Usa o mesmo <see cref="LicenseImportService"/>
/// de <c>PUT /api/v1/licensing/license</c>, com as mesmas validações e a mesma cadeia de eventos, e ator técnico
/// <c>installer</c>. A versão esperada é a lida no momento, porque não há leitura anterior de um usuário.
/// </summary>
public static class LicensingAdminCommands
{
    public const string ActorId = "installer";

    /// <summary>Chaves públicas da licença: as embutidas na imagem Business. Os testes trocam pelo trust store de teste.</summary>
    public static Func<TrustStore> TrustStoreFactory { get; set; } = () => EmbeddedTrustStore.Load();

    internal static async Task<int> RunAsync(string command, AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        if (context.Edition != ProductEdition.Business)
        {
            return await AdminCommandHost.UsageAsync(context, "Edição sem licenciamento: o comando só existe na imagem Business.").ConfigureAwait(false);
        }

        if (command != "import")
        {
            return await AdminCommandHost.UsageAsync(context, $"Comando desconhecido: licensing {command}.").ConfigureAwait(false);
        }

        arguments.OnlyAllow("file", "slot", "actor", "reason");
        var slot = arguments.Required("slot");
        if (slot is not ("prd" or "hml"))
        {
            throw new AdminUsageException("O slot precisa ser prd ou hml.");
        }

        _ = BootstrapCommands.Operator(arguments);
        var file = new FileInfo(arguments.Required("file"));
        if (!file.Exists || file.Length > JwsCompactReader.MaxDocumentBytes)
        {
            throw new AdminUsageException("O arquivo de licença não existe ou tem mais de 64 KiB.");
        }

        var content = await File.ReadAllBytesAsync(file.FullName, ct).ConfigureAwait(false);
        using var trustStore = TrustStoreFactory();
        var store = new LicenseStore(database, new LicensingAuditWriter(database));
        var service = new LicenseImportService(store, store, new LicenseStateEvaluator(trustStore), trustStore, TimeProvider.System);
        var version = (await store.ReadAsync(ct).ConfigureAwait(false))?.Version ?? 0;
        var result = await service.ImportAsync(content, slot, version, ActorId, Guid.NewGuid(), ct).ConfigureAwait(false);

        return result.Outcome switch
        {
            LicenseImportOutcome.Accepted or LicenseImportOutcome.Unchanged => await AdminCommandHost.WriteAsync(context, new
            {
                imported = result.Outcome == LicenseImportOutcome.Accepted,
                licenseId = result.Status?.License?.LicenseId,
                entitlement = result.Status?.Entitlement,
            }).ConfigureAwait(false),
            _ => await RejectedAsync(context, result.ProblemCode ?? LicenseImportCodes.RevisionConflict).ConfigureAwait(false),
        };
    }

    private static async Task<int> RejectedAsync(AdminContext context, string code)
    {
        await context.Error.WriteLineAsync($"Licença recusada: {code}.").ConfigureAwait(false);
        return await AdminCommandHost.WriteAsync(context, new { problemCode = code }, AdminExitCodes.Usage).ConfigureAwait(false);
    }
}
