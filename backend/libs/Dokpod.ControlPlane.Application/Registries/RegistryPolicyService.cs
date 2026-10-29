using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.Registries;

public sealed record RegistryPolicyView(EnvironmentStage? Stage, IReadOnlyList<string> Registries, bool FromConsole, string? UpdatedBy, DateTimeOffset? UpdatedAt, bool CanEdit);

public enum RegistryPolicyOutcome
{
    Saved,
    Invalid,
    Forbidden,
}

/// <summary>
/// Política de registros permitidos por estágio (spec 020 D-004): leitura por quem lê algum host; edição com
/// `environment:manage` em todos os hosts habilitados e legíveis do estágio (regra da spec 016); auditada por host.
/// </summary>
public sealed class RegistryPolicyService(
    IRegistryPolicyStore store,
    ImagePullPolicy policy,
    IEnvironmentAuthorizationDecider decider,
    IEnvironmentGrantReader grantReader,
    IAuditEventWriter auditWriter,
    TimeProvider timeProvider)
{
    public const int MaxRegistries = 100;

    public async Task<IReadOnlyList<RegistryPolicyView>> ListAsync(AlertCaller caller, CancellationToken cancellationToken)
    {
        var stored = (await store.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(p => RegistryPolicyStages.Key(p.Stage));
        policy.ApplyOverrides(stored.ToDictionary(p => p.Key, p => p.Value.Registries.ToArray()));
        // Leitura: uma só consulta ao provedor; salvar continua decidindo host a host.
        var managed = (await grantReader.WithScopeAsync(caller.Readable.Where(r => r.Enabled).DistinctBy(r => r.EnvironmentId), EnvironmentResourceScopes.Manage,
            caller.Actor, caller.AccessToken, caller.CorrelationId, cancellationToken).ConfigureAwait(false)).Select(r => r.EnvironmentId).ToHashSet();
        return RegistryPolicyStages.All.Select(stage =>
        {
            var key = RegistryPolicyStages.Key(stage);
            var saved = stored.GetValueOrDefault(key);
            var candidates = Candidates(caller, stage);
            return new RegistryPolicyView(stage, policy.AllowedFor(key), saved is not null, saved?.UpdatedBy, saved?.UpdatedAt,
                candidates.Count > 0 && candidates.All(r => managed.Contains(r.EnvironmentId)));
        }).ToArray();
    }

    /// <summary>Normaliza e valida a lista (gramática da spec 012, sem duplicados, até 100); nulo quando inválida.</summary>
    public static IReadOnlyList<string>? Normalize(IReadOnlyList<string>? registries)
    {
        if (registries is null || registries.Count > MaxRegistries) return null;
        var result = new List<string>();
        foreach (var value in registries)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            // Só host[:porta]: esquema, caminho e credencial são recusados (não apenas removidos).
            if (trimmed.Contains("://", StringComparison.Ordinal) || trimmed.Contains('/') || trimmed.Contains('@')) return null;
            if (RegistryNames.Normalize(trimmed) is not { } registry) return null;
            if (!result.Contains(registry, StringComparer.Ordinal)) result.Add(registry);
        }

        return result.Order(StringComparer.Ordinal).ToArray();
    }

    public async Task<RegistryPolicyOutcome> SaveAsync(AlertCaller caller, EnvironmentStage? stage, IReadOnlyList<string>? registries, CancellationToken cancellationToken)
    {
        if (Normalize(registries) is not { } normalized) return RegistryPolicyOutcome.Invalid;
        var candidates = Candidates(caller, stage);
        if (candidates.Count == 0) return RegistryPolicyOutcome.Forbidden;
        var managed = await ManagedAsync(caller, candidates, cancellationToken).ConfigureAwait(false);
        if (managed.Count != candidates.Count) return RegistryPolicyOutcome.Forbidden;
        var key = RegistryPolicyStages.Key(stage);
        var previous = policy.AllowedFor(key);
        var now = timeProvider.GetUtcNow();
        await store.SaveAsync(new StoredRegistryPolicy(stage, normalized, caller.Actor.Label, now), cancellationToken).ConfigureAwait(false);
        var stored = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        policy.ApplyOverrides(stored.ToDictionary(p => RegistryPolicyStages.Key(p.Stage), p => p.Registries.ToArray()));
        static string Text(IReadOnlyCollection<string> list) => list.Count == 0 ? "*" : string.Join(", ", list);
        foreach (var host in candidates)
        {
            var auditEvent = AuditEvent.Create(Guid.NewGuid(), now, caller.CorrelationId, AuditActorKind.User, caller.Actor.Subject, "registry.policy_changed", host.EnvironmentId, AuditOutcome.Succeeded, null)
                .WithActorName(caller.Actor.DisplayName)
                .WithSubject(key, new Dictionary<string, string> { ["stage"] = key, ["previousRegistries"] = Text(previous), ["registries"] = Text(normalized) });
            await auditWriter.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);
        }

        return RegistryPolicyOutcome.Saved;
    }

    /// <summary>Hosts habilitados e legíveis do estágio; sem nenhum, todos os legíveis (mesma regra da spec 016).</summary>
    private static IReadOnlyList<EnvironmentRegistration> Candidates(AlertCaller caller, EnvironmentStage? stage)
    {
        var inStage = caller.Readable.Where(r => r.Enabled && r.Stage == stage).ToArray();
        return inStage.Length > 0 ? inStage : caller.Readable.Where(r => r.Enabled).ToArray();
    }

    private async Task<IReadOnlySet<Guid>> ManagedAsync(AlertCaller caller, IEnumerable<EnvironmentRegistration> candidates, CancellationToken cancellationToken)
    {
        var hosts = candidates.Where(r => r.Enabled).DistinctBy(r => r.EnvironmentId).ToArray();
        var decisions = await Task.WhenAll(hosts.Select(async registration =>
        {
            try
            {
                return (await decider.DecideAsync($"urn:dokpod:environment:{registration.EnvironmentId:D}", EnvironmentResourceScopes.Manage,
                    caller.Actor, caller.AccessToken, caller.CorrelationId, cancellationToken).ConfigureAwait(false)).Allowed;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        })).ConfigureAwait(false);
        return hosts.Where((_, index) => decisions[index]).Select(r => r.EnvironmentId).ToHashSet();
    }
}
