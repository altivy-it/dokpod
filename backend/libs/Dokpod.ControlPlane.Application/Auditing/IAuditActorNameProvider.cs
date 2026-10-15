namespace Dokpod.ControlPlane.Application.Auditing;

/// <summary>Nome exibível do usuário que está agindo (spec 017 D-001), consultado pelo writer de auditoria.</summary>
public interface IAuditActorNameProvider
{
    /// <summary>Nome do usuário autenticado quando o identificador dele é <paramref name="actorId"/>; senão nulo.</summary>
    string? NameFor(string actorId);
}
