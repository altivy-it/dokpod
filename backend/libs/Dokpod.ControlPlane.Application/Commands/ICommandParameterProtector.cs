namespace Dokpod.ControlPlane.Application.Commands;

/// <summary>
/// Cifra os valores literais de comandos parametrizados (spec 013 D-002) com o key ring próprio da API (D-006).
/// `TryUnprotect` devolve nulo quando a chave não existe mais (perda do key ring → `parameters_unavailable`).
/// </summary>
public interface ICommandParameterProtector
{
    byte[] Protect(byte[] plaintext);

    byte[]? TryUnprotect(byte[] protectedData);
}
