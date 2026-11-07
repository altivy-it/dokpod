using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T021/T022: o provedor da condição de alerta (gate ao criar ou editar) e a fonte da varredura com PostgreSQL
/// real, com o mesmo estado por container das listas (<c>container_behind</c> pela imagem local, D-008).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageOutdatedAlertsTests : IAsyncLifetime
{
    private const string Nginx = "docker.io/library/nginx:1.27";
    private const string Checkout = "registry.empresa.com/loja/checkout:5.2.0";
    private static readonly string Current = "sha256:" + new string('a', 64);
    private static readonly string Previous = "sha256:" + new string('b', 64);
    private static readonly string CheckoutImage = "sha256:" + new string('c', 64);
    private readonly Guid environmentId = Guid.NewGuid();
    private ControlPlaneDbContext? context;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t025a-{environmentId:N}"[..20], "host-t025a", true, [EnvironmentResourceScopes.Read]), Ct);
        await Execute("INSERT INTO dokpod.inventory_projections (environment_id, revision, observed_at_utc) VALUES (@env, 1, now())");
        foreach (var (id, name, reference, image) in new[] { ("c-web-1", "web-1", Nginx, Previous), ("c-web-2", "web-2", Nginx, Current), ("c-api", "api", Checkout, CheckoutImage) })
        {
            await Execute(
                """
                INSERT INTO dokpod.inventory_containers (environment_id, container_id, name, image_reference, state, revision, observed_at_utc, image_id)
                VALUES (@env, @id, @name, @reference, 'running', 'r1', now(), @image)
                """,
                new NpgsqlParameter("id", id), new NpgsqlParameter("name", name), new NpgsqlParameter("reference", reference), new NpgsqlParameter("image", image));
        }
    }

    [Fact]
    public async Task Source_DerivesTheContainerState_AndKeepsOutdatedSinceOfTheReference()
    {
        var now = DateTimeOffset.UtcNow;
        await new ImageUpdateStore(context!).UpsertLocalAsync(
            environmentId,
            [
                new ImageUpdateResult(Nginx, "linux/amd64", ImageUpdateStates.UpToDate, null, "sha256:" + new string('d', 64), Current),
                new ImageUpdateResult(Checkout, "linux/amd64", ImageUpdateStates.UpdateAvailable, null, "sha256:" + new string('e', 64), CheckoutImage),
            ],
            now,
            Ct);

        var targets = (await Source(licensed: true).ListAsync([environmentId], Ct))!.ToDictionary(t => t.ContainerName);

        Assert.Equal((ImageUpdateStates.ContainerBehind, (DateTimeOffset?)null), (targets["web-1"].State, targets["web-1"].OutdatedSinceUtc));
        Assert.Equal(ImageUpdateStates.UpToDate, targets["web-2"].State);
        Assert.Equal((ImageUpdateStates.UpdateAvailable, Checkout, "c-api"), (targets["api"].State, targets["api"].Reference, targets["api"].ContainerId));
        Assert.NotNull(targets["api"].OutdatedSinceUtc);
    }

    [Fact]
    public async Task Source_WithoutTheCapabilityInTheLicense_ReturnsNothingToEvaluate()
    {
        Assert.Null(await Source(licensed: false).ListAsync([environmentId], Ct));
        Assert.Empty((await Source(licensed: true).ListAsync([], Ct))!);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("license_expired", "license_expired")]
    [InlineData("license_invalid", "license_invalid")]
    public async Task Availability_UsesTheGateForCreateOrEdit(string? denial, string? expected)
    {
        var availability = new ImageOutdatedConditionAvailability(new Gate(denial), new Entitlement(true));

        Assert.True(availability.Supports(AlertConditionKind.ImageOutdated));
        Assert.False(availability.Supports(AlertConditionKind.Metric));
        Assert.Equal(expected, await availability.EnsureAllowedAsync(AlertConditionKind.ImageOutdated, Ct));
        Assert.True(await availability.IsOfferedAsync(AlertConditionKind.ImageOutdated, Ct));
        Assert.False(await new ImageOutdatedConditionAvailability(new Gate(null), new Entitlement(false)).IsOfferedAsync(AlertConditionKind.ImageOutdated, Ct));
    }

    public async ValueTask DisposeAsync()
    {
        if (context is null) return;
        await Execute("DELETE FROM dokpod.image_update_checks WHERE environment_id = @env");
        await Execute("DELETE FROM dokpod.inventory_projections WHERE environment_id = @env");
        await context.DisposeAsync();
    }

    private ImageOutdatedSource Source(bool licensed) => new(new ImageUpdateStore(context!), new ImageUpdateTargetReader(context!), new Entitlement(licensed));

    private Task<int> Execute(string sql, params NpgsqlParameter[] parameters) =>
        context!.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("env", environmentId), .. parameters], Ct);

    private sealed class Entitlement(bool licensed) : IImageUpdateEntitlement
    {
        public Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) => Task.FromResult(licensed);
    }

    private sealed class Gate(string? denial) : IBusinessCapabilityGate
    {
        public Task<BusinessCapabilityDecision> EnsureAsync(string capability, CancellationToken cancellationToken) =>
            Task.FromResult(denial is null ? BusinessCapabilityDecision.Allow : BusinessCapabilityDecision.Deny(denial, []));
    }
}
