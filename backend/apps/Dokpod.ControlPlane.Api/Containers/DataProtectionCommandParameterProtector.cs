using System.Security.Cryptography;
using Dokpod.ControlPlane.Application.Commands;
using Microsoft.AspNetCore.DataProtection;

namespace Dokpod.ControlPlane.Api;

/// <summary>Valores de comandos cifrados com o key ring próprio da API (spec 013 D-006), propósito versionado.</summary>
public sealed class DataProtectionCommandParameterProtector(IDataProtectionProvider provider) : ICommandParameterProtector
{
    public const string Purpose = "Dokpod.CommandParameters.v1";

    private readonly IDataProtector protector = provider.CreateProtector(Purpose);

    public byte[] Protect(byte[] plaintext) => protector.Protect(plaintext);

    public byte[]? TryUnprotect(byte[] protectedData)
    {
        try
        {
            return protector.Unprotect(protectedData);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
