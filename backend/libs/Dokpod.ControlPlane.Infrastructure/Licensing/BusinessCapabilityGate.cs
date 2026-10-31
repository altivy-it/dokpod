using Dokpod.ControlPlane.Application.Licensing;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Gate dos recursos exclusivos Business (spec 031 T046, FR-021). Reavalia o estado a cada nova operação ou execução
/// de job, sem cache que estenda prazo. Só decide o direito comercial: a autorização do Keycloak vem antes (FR-022),
/// operações já aceitas concluem e nada aqui para containers ou bloqueia a CE. Registrado só na composição Business.
/// </summary>
public sealed class BusinessCapabilityGate(LicenseStatusService statusService) : IBusinessCapabilityGate
{
    public async Task<BusinessCapabilityDecision> EnsureAsync(string capability, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        var status = await statusService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (status.Entitlement is "granted" or "granted_in_grace"
            && status.License?.Capabilities.Contains(capability, StringComparer.Ordinal) == true)
        {
            return BusinessCapabilityDecision.Allow;
        }

        return BusinessCapabilityDecision.Deny(DenialCode(status), status.Reasons);
    }

    public static string DenialCode(LicenseStatus status) => status switch
    {
        { Entitlement: "indeterminate" } => "license_state_indeterminate",
        { Authenticity: "revoked" } => "license_revoked",
        // Expiração vem antes do excesso: sem licença vigente, o excesso não importa.
        _ when status.Reasons.Contains(LicenseReasons.Expired) && status.Entitlement == "restricted" => "license_expired",
        _ when status.Reasons.Contains(LicenseReasons.CapacityExceeded) && status.Entitlement == "restricted" => "license_capacity_exceeded",
        // Sem licença, licença inválida, ainda não vigente ou capability fora da licença.
        _ => "license_invalid",
    };
}
