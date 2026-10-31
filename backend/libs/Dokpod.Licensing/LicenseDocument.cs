// SPDX-License-Identifier: MIT
namespace Dokpod.Licensing;

/// <summary>Capacidade contratada: <c>limited</c> com nodes equivalentes, ou <c>unlimited</c> explícito (nunca zero).</summary>
public sealed record LicenseCapacity(bool IsUnlimited, int? NodeEquivalents);

/// <summary>Slot de instalação. Desabilitado (ou com cota 0) não usa cota; <c>NodeEquivalents</c> é null em licença ilimitada.</summary>
public sealed record LicenseSlot(string SlotId, string Kind, bool Enabled, int? NodeEquivalents)
{
    public bool IsDisabled => !Enabled || NodeEquivalents == 0;
}

/// <summary>Payload verificado da licença (data-model.md, perfil 1). Imutável; só existe depois da assinatura conferida.</summary>
public sealed record LicenseDocument(
    int SchemaVersion,
    int MeteringVersion,
    string LicenseId,
    string OrganizationId,
    int Revision,
    string Product,
    string Plan,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    LicenseCapacity Capacity,
    int VcpuBand,
    int WindowDays,
    int GraceDays,
    IReadOnlyList<LicenseSlot> Slots,
    IReadOnlyList<string> Capabilities)
{
    public LicenseSlot? FindSlot(string slotId) => Slots.FirstOrDefault(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));
}

/// <summary>Capabilities conhecidas por esta versão do produto; cada uma corresponde a uma spec Business (025 a 029).</summary>
public static class LicenseCapabilities
{
    public const string ImageUpdateNotice = "image-update-notice";
    public const string ChangeWindows = "change-windows";
    public const string AuditSiemExport = "audit-siem-export";
    public const string CodeOnlyDeployment = "code-only-deployment";
    public const string SsoTemplates = "sso-templates";

    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(
        [ImageUpdateNotice, ChangeWindows, AuditSiemExport, CodeOnlyDeployment, SsoTemplates],
        StringComparer.Ordinal);
}
