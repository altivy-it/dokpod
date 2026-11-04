using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.ControlPlane.Application.Registries;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

/// <summary>
/// Referência em uso por um container do host, a imagem local dele e a plataforma (<c>os/arquitetura</c> da imagem no
/// inventário ou, sem ela, do agente), usada nos resultados decididos no plano de controle (inventário, spec 012).
/// </summary>
public sealed record ImageUpdateTarget(string Reference, string? ImageId, string Platform);

/// <summary>Referências em uso por host, lidas da projeção do inventário; nunca do agente.</summary>
public interface IImageUpdateTargetReader
{
    Task<IReadOnlyList<ImageUpdateTarget>> ListAsync(Guid environmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Imagem local que hoje tem cada referência (tag no inventário de imagens), para derivar <c>container_behind</c>
    /// (D-008): forma canônica da referência → <c>image_id</c>.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LocalImageIdsAsync(Guid environmentId, CancellationToken cancellationToken);
}

/// <summary>Canal da mensagem 17 até o agente conectado (D-004).</summary>
public interface IImageUpdateAgentChannel
{
    bool IsConnected(Guid environmentId);

    bool Supports(Guid environmentId);

    Task<bool> SendAsync(Guid environmentId, ImageUpdateCheckRequest request, CancellationToken cancellationToken);
}

/// <summary>Canal pelo <see cref="AgentQueryDispatcher"/>, que já guarda as capabilities da sessão.</summary>
public sealed class DispatcherImageUpdateAgentChannel(AgentQueryDispatcher dispatcher) : IImageUpdateAgentChannel
{
    public bool IsConnected(Guid environmentId) => dispatcher.IsConnected(environmentId);

    public bool Supports(Guid environmentId) => dispatcher.Supports(environmentId, Capability.ImageUpdateCheck);

    public Task<bool> SendAsync(Guid environmentId, ImageUpdateCheckRequest request, CancellationToken cancellationToken) =>
        dispatcher.TrySendAsync(environmentId, new ControlPlaneMessage { ImageUpdateCheck = request }, Capability.ImageUpdateCheck, cancellationToken);
}

/// <summary>A licença importada lista <c>image-update-notice</c>? Com a licença restrita, a resposta continua sim (FR-009).</summary>
public interface IImageUpdateEntitlement
{
    Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken);
}

public sealed class LicenseImageUpdateEntitlement(LicenseStatusService statusService) : IImageUpdateEntitlement
{
    public const string Capability = "image-update-notice";

    public async Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) =>
        (await statusService.GetAsync(cancellationToken).ConfigureAwait(false)).License?.Capabilities.Contains(Capability, StringComparer.Ordinal) == true;
}

/// <summary>
/// Agenda da verificação (spec 025 T013, D-006): a réplica que pega o lease de um host vencido monta os itens a
/// partir do inventário, grava direto <c>registry_not_allowed</c> (fora da política do estágio) e
/// <c>agent_unsupported</c> (agente sem a capability), envia a mensagem 17 e marca o próximo vencimento entre
/// 5 h 24 min e 6 h. Host desconectado continua vencido. Sem a capability na licença importada, não agenda; o
/// worker não consulta o gate, então a licença restrita não interrompe o aviso (FR-009).
/// </summary>
public sealed class ImageUpdateScheduler(
    IImageUpdateStore store,
    IImageUpdateTargetReader targets,
    IImageUpdateAgentChannel agents,
    IImageUpdateEntitlement entitlement,
    IEnvironmentRegistrationStore registrations,
    IRegistryPolicyStore registryPolicies,
    ImagePullPolicy pullPolicy,
    TimeProvider timeProvider,
    Func<double>? jitter = null)
{
    public const int MaxDuePerCycle = 100;

    /// <summary>Um ciclo; devolve quantos pedidos foram enviados.</summary>
    public async Task<int> RunOnceAsync(string owner, CancellationToken cancellationToken)
    {
        if (!await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false)) return 0;
        var hosts = await ListRegistrationsAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        await store.EnsureScheduledAsync(hosts.Keys.ToArray(), now, cancellationToken).ConfigureAwait(false);
        var stored = await registryPolicies.ListAsync(cancellationToken).ConfigureAwait(false);
        pullPolicy.ApplyOverrides(stored.ToDictionary(p => RegistryPolicyStages.Key(p.Stage), p => p.Registries.ToArray()));

        var sent = 0;
        foreach (var environmentId in await store.ListDueAsync(now, MaxDuePerCycle, cancellationToken).ConfigureAwait(false))
        {
            if (!hosts.TryGetValue(environmentId, out var host) || !await store.TryAcquireAsync(environmentId, owner, now, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            try
            {
                if (await DispatchAsync(host, owner, now, cancellationToken).ConfigureAwait(false)) sent++;
            }
            catch
            {
                await store.ReleaseAsync(environmentId, owner, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        return sent;
    }

    private async Task<bool> DispatchAsync(Dokpod.Domain.Environments.EnvironmentRegistration host, string owner, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var environmentId = host.EnvironmentId;
        if (!agents.IsConnected(environmentId))
        {
            await store.ReleaseAsync(environmentId, owner, cancellationToken).ConfigureAwait(false);
            return false;
        }

        var local = new List<ImageUpdateResult>();
        var items = new List<ImageUpdateCheckItem>();
        var supported = agents.Supports(environmentId);
        foreach (var target in (await targets.ListAsync(environmentId, cancellationToken).ConfigureAwait(false)).DistinctBy(t => t.Reference).Take(ImageUpdateLimits.MaxItems))
        {
            if (!ImageReference.TryParse(target.Reference, out var reference)) continue;
            var reason = !pullPolicy.Allows(host.Stage, reference) ? ImageUpdateReasons.RegistryNotAllowed
                : !supported ? ImageUpdateReasons.AgentUnsupported
                : null;
            if (reason is not null)
            {
                local.Add(new ImageUpdateResult(target.Reference, target.Platform, ImageUpdateStates.NotCheckable, reason, null, target.ImageId));
                continue;
            }

            items.Add(new ImageUpdateCheckItem { Reference = target.Reference, ImageId = target.ImageId ?? string.Empty });
        }

        if (local.Count > 0) await store.UpsertLocalAsync(environmentId, local, now, cancellationToken).ConfigureAwait(false);
        var requestId = Guid.NewGuid();
        if (items.Count > 0)
        {
            var request = new ImageUpdateCheckRequest { RequestId = requestId.ToString("D") };
            request.Items.AddRange(items);
            if (!await agents.SendAsync(environmentId, request, cancellationToken).ConfigureAwait(false))
            {
                // Sessão caiu entre a checagem e o envio: o host continua vencido.
                await store.ReleaseAsync(environmentId, owner, cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        var next = ImageUpdateLimits.NextDue(now, (jitter ?? Random.Shared.NextDouble)());
        return await store.MarkRequestedAsync(environmentId, owner, requestId, now, next, cancellationToken).ConfigureAwait(false) && items.Count > 0;
    }

    private async Task<Dictionary<Guid, Dokpod.Domain.Environments.EnvironmentRegistration>> ListRegistrationsAsync(CancellationToken cancellationToken)
    {
        var all = new Dictionary<Guid, Dokpod.Domain.Environments.EnvironmentRegistration>();
        Guid? cursor = null;
        do
        {
            var page = await registrations.ListAsync(cursor, 100, cancellationToken).ConfigureAwait(false);
            foreach (var registration in page.Registrations.Where(r => r.Enabled)) all[registration.EnvironmentId] = registration;
            cursor = page.NextCursor;
        }
        while (cursor is not null && all.Count < 10_000);

        return all;
    }
}
