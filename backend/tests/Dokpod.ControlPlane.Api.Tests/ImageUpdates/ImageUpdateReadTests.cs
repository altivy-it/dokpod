using System.Text.Json;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T015 com PostgreSQL real: <c>updateCheck</c> das listas sai dos resultados gravados, com
/// <c>container_behind</c> derivado, <c>stale</c> com o host desconectado e nada sem a capability na licença.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImageUpdateReadTests : IAsyncLifetime
{
    private const string Reference = "docker.io/library/nginx:1.28";
    private static readonly string Remote = "sha256:" + new string('b', 64);
    private static readonly string LocalImage = "sha256:" + new string('c', 64);
    private static readonly string OldImage = "sha256:" + new string('d', 64);
    private readonly Guid environmentId = Guid.NewGuid();
    private ControlPlaneDbContext? context;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        context = NetworksIntegrationTests.CreateContextOrSkip();
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(environmentId, $"t025l-{environmentId:N}"[..20], "host-t025l", true, [EnvironmentResourceScopes.Read]), Ct);
    }

    [Fact]
    public async Task UpToDateReference_WithOtherLocalImage_IsContainerBehind_AndTheImageIsUpToDate()
    {
        await Store.UpsertLocalAsync(environmentId, [new ImageUpdateResult(Reference, "linux/amd64", ImageUpdateStates.UpToDate, null, Remote, LocalImage)], DateTimeOffset.UtcNow, Ct);

        var lookup = (await Reader(licensed: true).LoadAsync([environmentId], Ct))!;

        Assert.Equal(ImageUpdateStates.ContainerBehind, lookup.ForContainer(environmentId, "nginx:1.28", OldImage, stale: false)!.State);
        Assert.Equal(ImageUpdateStates.UpToDate, lookup.ForContainer(environmentId, Reference, LocalImage, stale: false)!.State);
        Assert.Equal(ImageUpdateStates.UpToDate, lookup.ForImage(environmentId, ["outra:1", "nginx:1.28"], stale: false)!.State);
        Assert.True(lookup.ForContainer(environmentId, Reference, LocalImage, stale: true)!.Stale);
        Assert.Null(lookup.ForContainer(environmentId, "redis:7", LocalImage, stale: false));
    }

    [Fact]
    public async Task WorstStateAcrossPlatforms_Wins_AndTheViewNeverCarriesTheRemoteDigest()
    {
        var now = DateTimeOffset.UtcNow;
        await Store.UpsertLocalAsync(environmentId,
        [
            new ImageUpdateResult(Reference, "linux/amd64", ImageUpdateStates.UpToDate, null, Remote, LocalImage),
            new ImageUpdateResult(Reference, "linux/arm64", ImageUpdateStates.UpdateAvailable, null, Remote, LocalImage),
        ], now, Ct);

        var view = (await Reader(licensed: true).LoadAsync([environmentId], Ct))!.ForContainer(environmentId, Reference, LocalImage, stale: false)!;
        var json = JsonSerializer.Serialize(view, JsonSerializerOptions.Web);

        Assert.Equal(ImageUpdateStates.UpdateAvailable, view.State);
        Assert.DoesNotContain(Remote, json, StringComparison.Ordinal);
        Assert.DoesNotContain("digest", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"checkedAt\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nginx:1.28", "docker.io/library/nginx:1.28")]
    [InlineData("docker.io/nginx:1.28", "nginx:1.28")]
    [InlineData("library/nginx", "nginx:latest")]
    [InlineData("registry.empresa.com/app/api:3", "registry.empresa.com/app/api:3")]
    public async Task PageLoad_ReadsOnlyThePageReferences_InAnySpellingOfTheSameCanonical(string stored, string onThePage)
    {
        // SC-004: a lista lê só as referências da página, mas casa a grafia gravada (a do container) pela forma canônica.
        var now = DateTimeOffset.UtcNow;
        await Store.UpsertLocalAsync(environmentId,
        [
            new ImageUpdateResult(stored, "linux/amd64", ImageUpdateStates.UpdateAvailable, null, Remote, LocalImage),
            new ImageUpdateResult("redis:7", "linux/amd64", ImageUpdateStates.UpdateAvailable, null, Remote, LocalImage),
        ], now, Ct);

        var lookup = (await Reader(licensed: true).LoadAsync([environmentId], [onThePage], Ct))!;

        Assert.Equal(ImageUpdateStates.UpdateAvailable, lookup.ForImage(environmentId, [onThePage], stale: false)!.State);
        Assert.Equal(ImageUpdateStates.UpdateAvailable, lookup.ForContainer(environmentId, onThePage, LocalImage, stale: false)!.State);
        Assert.Null(lookup.ForContainer(environmentId, "redis:7", LocalImage, stale: false));
    }

    [Fact]
    public async Task WithoutTheCapabilityInTheLicense_TheFieldIsAbsent()
    {
        await Store.UpsertLocalAsync(environmentId, [new ImageUpdateResult(Reference, "linux/amd64", ImageUpdateStates.UpdateAvailable, null, Remote, LocalImage)], DateTimeOffset.UtcNow, Ct);

        Assert.Null(await Reader(licensed: false).LoadAsync([environmentId], Ct));
    }

    private ImageUpdateStore Store => new(context!);

    private ImageUpdateReadService Reader(bool licensed) => new(Store, new Entitlement(licensed));

    public async ValueTask DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
    }

    private sealed class Entitlement(bool licensed) : IImageUpdateEntitlement
    {
        public Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) => Task.FromResult(licensed);
    }
}
