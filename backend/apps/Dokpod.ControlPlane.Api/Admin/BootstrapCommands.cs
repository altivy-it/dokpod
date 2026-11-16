using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Api.Admin;

/// <summary>
/// Spec 033 T015 (FR-024, FR-025, D-004): <c>environments register</c> e <c>agent-identities register|revoke</c> pelo
/// <see cref="AgentIdentityRegistrationService"/>, com auditoria do ator técnico <c>installer</c>. Os recursos de
/// autorização no Keycloak ficam com o instalador (<c>provision-environment-authorization.ps1</c>).
/// </summary>
internal static class BootstrapCommands
{
    public static async Task<int> RunAsync(
        string group,
        string command,
        AdminArguments arguments,
        ControlPlaneDbContext database,
        AdminContext context,
        CancellationToken ct)
    {
        var identities = new PostgresAgentIdentityRegistry(database);
        var service = new AgentIdentityRegistrationService(
            new PostgresEnvironmentRegistrationStore(database), identities, identities, new PostgresAuditEventWriter(database));
        var correlationId = Guid.NewGuid();

        switch (group, command)
        {
            case ("environments", "register"):
            {
                arguments.OnlyAllow("id", "name", "host", "stage", "actor", "reason");
                var registration = Registration(arguments);
                var result = await service.RegisterEnvironmentAsync(registration, Operator(arguments), correlationId, ct).ConfigureAwait(false);
                return await ResultAsync(context, result, new { environmentId = registration.EnvironmentId, created = result.Changed }).ConfigureAwait(false);
            }

            case ("agent-identities", "register"):
            {
                arguments.OnlyAllow("environment", "fingerprint", "actor", "reason");
                var environmentId = arguments.RequiredGuid("environment");
                string fingerprint;
                try
                {
                    fingerprint = AgentIdentityRegistrationService.NormalizeFingerprint(arguments.Required("fingerprint"));
                }
                catch (ArgumentException error)
                {
                    throw new AdminUsageException(error.Message);
                }

                var result = await service.RegisterIdentityAsync(environmentId, fingerprint, Operator(arguments), correlationId, ct).ConfigureAwait(false);
                return await ResultAsync(context, result, new { environmentId, fingerprint, created = result.Changed }).ConfigureAwait(false);
            }

            case ("agent-identities", "revoke"):
            {
                arguments.OnlyAllow("environment", "actor", "reason");
                var environmentId = arguments.RequiredGuid("environment");
                var result = await service.RevokeIdentityAsync(environmentId, Operator(arguments), correlationId, ct).ConfigureAwait(false);
                return await ResultAsync(context, result, new { environmentId, revoked = result.Changed }).ConfigureAwait(false);
            }

            default:
                return await AdminCommandHost.UsageAsync(context, $"Comando desconhecido: {group} {command}.").ConfigureAwait(false);
        }
    }

    private static async Task<int> ResultAsync(AdminContext context, InstallerCommandResult result, object output)
    {
        if (result.Succeeded)
        {
            return await AdminCommandHost.WriteAsync(context, output).ConfigureAwait(false);
        }

        await context.Error.WriteLineAsync($"Recusado: {result.FailureCode}.").ConfigureAwait(false);
        var exitCode = result.FailureCode == "environment_not_found" ? AdminExitCodes.Usage : AdminExitCodes.IdentityConflict;
        return await AdminCommandHost.WriteAsync(context, new { failureCode = result.FailureCode }, exitCode).ConfigureAwait(false);
    }

    private static EnvironmentRegistration Registration(AdminArguments arguments)
    {
        var stage = arguments.Required("stage") switch
        {
            "production" => EnvironmentStage.Production,
            "staging" => EnvironmentStage.Staging,
            "development" => EnvironmentStage.Development,
            "none" => (EnvironmentStage?)null,
            var other => throw new AdminUsageException($"Estágio desconhecido: {other}."),
        };

        try
        {
            return EnvironmentRegistration.Create(
                arguments.RequiredGuid("id"), arguments.Required("name"), arguments.Required("host"), enabled: true,
                EnvironmentResourceScopes.Supported, stage);
        }
        catch (ArgumentException error)
        {
            throw new AdminUsageException(error.Message);
        }
    }

    internal static InstallerOperator Operator(AdminArguments arguments) => new(arguments.Required("actor"), arguments.Required("reason"));
}
