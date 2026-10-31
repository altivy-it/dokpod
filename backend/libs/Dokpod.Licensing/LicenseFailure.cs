// SPDX-License-Identifier: MIT
namespace Dokpod.Licensing;

/// <summary>Falhas tipadas do verificador (contracts/license-format.md, spec 031).</summary>
public enum LicenseFailure
{
    /// <summary>Envelope, Base64URL, UTF-8 ou JSON inválido; limite excedido; propriedade duplicada ou desconhecida.</summary>
    Malformed,

    /// <summary><c>alg</c>, <c>typ</c>, header extra, <c>schemaVersion</c> ou <c>meteringVersion</c> não suportados.</summary>
    UnsupportedProfile,

    /// <summary><c>kid</c> fora do trust store.</summary>
    UnknownKey,

    /// <summary>Assinatura não confere.</summary>
    BadSignature,

    /// <summary><c>product</c> diferente de <c>dokpod-business</c>.</summary>
    WrongProduct,

    /// <summary>Ordem de datas inválida.</summary>
    InconsistentDates,

    /// <summary>Capacidade, faixa, slots ou soma de cotas inválidos.</summary>
    InvalidCapacity,

    /// <summary>Capability fora da allowlist da versão ou acima do limite.</summary>
    UnknownCapability,
}

public static class LicenseFailureCodes
{
    public static string ToCode(this LicenseFailure failure) => failure switch
    {
        LicenseFailure.Malformed => "malformed",
        LicenseFailure.UnsupportedProfile => "unsupported_profile",
        LicenseFailure.UnknownKey => "unknown_key",
        LicenseFailure.BadSignature => "bad_signature",
        LicenseFailure.WrongProduct => "wrong_product",
        LicenseFailure.InconsistentDates => "inconsistent_dates",
        LicenseFailure.InvalidCapacity => "invalid_capacity",
        LicenseFailure.UnknownCapability => "unknown_capability",
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };
}
