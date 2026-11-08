using System.Diagnostics;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T025 (SC-004): primeira página de Imagens e de Containers, com o caminho das rotas (busca no inventário,
/// leitura dos resultados da página numa consulta e serialização com <c>updateCheck</c>). Dois hosts têm o mesmo
/// inventário de 200 imagens e 200 containers; só um tem as 200 imagens verificadas. As medições se alternam, 30 por
/// lado depois de 5 de aquecimento, e a comparação roda cinco vezes; vale a mediana das razões dos p95, como no SC-004
/// da 027. A margem de 10% fica perto da variação da cauda numa máquina com CPU disputada, então a medição roda só
/// quando pedida, isolada: <c>DOKPOD_TEST_SC025=1</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresPerformanceCollection.Name)]
public sealed class ImageUpdateListImpactTests
{
    private const int Images = 200;
    private const int Runs = 30;
    private const int WarmUp = 5;
    private const int Trials = 5;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstPageOfImagesAndContainers_With200CheckedImages_StaysWithin10PercentOfTheP95WithoutChecks()
    {
        if (Environment.GetEnvironmentVariable("DOKPOD_TEST_SC025") != "1") Assert.Skip("Medição de SC-004 da 025 só isolada (DOKPOD_TEST_SC025=1).");
        await using var context = NetworksIntegrationTests.CreateContextOrSkip();
        var checkedHost = Guid.NewGuid();
        var plainHost = Guid.NewGuid();
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var host in new[] { checkedHost, plainHost }) await SeedAsync(context, host, now);
            await new ImageUpdateStore(context).UpsertLocalAsync(checkedHost, Enumerable.Range(0, Images).Select(i => new ImageUpdateResult(
                Reference(i), "linux/amd64", i % 4 == 0 ? ImageUpdateStates.UpdateAvailable : ImageUpdateStates.UpToDate, null, Digest('b', i), Digest('a', i))).ToArray(), now, Ct);

            var reader = new ImageUpdateReadService(new ImageUpdateStore(context), new Licensed());
            var images = new PostgresImageInventoryStore(context);
            var containers = new PostgresContainerSearchStore(context);
            var withData = await ImagesPageAsync(images, reader, checkedHost);
            Assert.Contains("\"update_available\"", withData);
            Assert.DoesNotContain("updateCheck\":{", await ImagesPageAsync(images, reader, plainHost));

            var ratios = new Dictionary<string, List<double>> { ["Imagens"] = [], ["Containers"] = [] };
            for (var trial = 1; trial <= Trials; trial++)
            {
                foreach (var (page, measure) in new (string, Func<Guid, Task<string>>)[]
                {
                    ("Imagens", host => ImagesPageAsync(images, reader, host)),
                    ("Containers", host => ContainersPageAsync(containers, reader, host)),
                })
                {
                    var off = new List<double>();
                    var on = new List<double>();
                    for (var round = 0; round < WarmUp + Runs; round++)
                    {
                        // Ordem alternada: rodadas pares começam sem verificação, ímpares com.
                        foreach (var withChecks in round % 2 == 0 ? new[] { false, true } : [true, false])
                        {
                            var watch = Stopwatch.StartNew();
                            await measure(withChecks ? checkedHost : plainHost);
                            if (round >= WarmUp) (withChecks ? on : off).Add(watch.Elapsed.TotalMilliseconds);
                        }
                    }

                    var (p95Off, p95On) = (P95(off), P95(on));
                    ratios[page].Add(p95On / p95Off);
                    TestContext.Current.TestOutputHelper?.WriteLine(
                        $"SC-004 025 {page}, comparação {trial}: p95 sem verificação={p95Off:0.00} ms, com 200 verificadas={p95On:0.00} ms ({p95On / p95Off:P1})");
                }
            }

            foreach (var (page, values) in ratios)
            {
                var ratio = Percentile(values, 0.50);
                TestContext.Current.TestOutputHelper?.WriteLine($"SC-004 025 {page}: mediana das razões p95 com/sem = {ratio:P1}");
                Assert.True(ratio <= 1.10, $"{page}: p95 com verificação ficou em {ratio:P1} do p95 sem (mediana de {Trials} comparações), acima de 110%");
            }
        }
        finally
        {
            Guid[] ids = [checkedHost, plainHost];
            await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.image_update_checks WHERE environment_id = ANY ({ids})", Ct);
            await context.InventoryProjections.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(Ct);
            await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(Ct);
        }
    }

    /// <summary>Mesmo trabalho da rota <c>GET /images</c> depois da autorização: busca, resultados da página e JSON.</summary>
    private static async Task<string> ImagesPageAsync(PostgresImageInventoryStore store, IImageUpdateAnnotations reader, Guid host)
    {
        var result = await store.SearchAsync(new ImageSearchQuery([host], false, null, ImageSort.NameAsc, 0, 50), Ct);
        var updates = await reader.LoadAsync(result.Rows.Select(r => r.EnvironmentId), result.Rows.SelectMany(r => r.Image.RepoTags), Ct);
        return JsonSerializer.Serialize(result.Rows.Select(row => new
        {
            imageId = row.Image.ImageId,
            repoTags = row.Image.RepoTags,
            sizeBytes = row.Image.SizeBytes,
            containers = row.Containers,
            updateCheck = updates?.ForImage(row.EnvironmentId, row.Image.RepoTags, stale: false),
        }), JsonSerializerOptions.Web);
    }

    /// <summary>Mesmo trabalho da rota <c>GET /containers</c> depois da autorização: busca, resultados da página e JSON.</summary>
    private static async Task<string> ContainersPageAsync(PostgresContainerSearchStore store, IImageUpdateAnnotations reader, Guid host)
    {
        var result = await store.SearchAsync(new ContainerSearchQuery([host], [], null, ContainerSort.NameAsc, null, 50), Ct);
        var updates = await reader.LoadAsync(result.Rows.Select(r => r.EnvironmentId), result.Rows.Select(r => r.Container.ImageReference), Ct);
        return JsonSerializer.Serialize(result.Rows.Select(row => new
        {
            containerId = row.Container.ContainerId,
            name = row.Container.Name,
            image = row.Container.ImageReference,
            state = row.Container.State,
            updateCheck = updates?.ForContainer(row.EnvironmentId, row.Container.ImageReference, row.Container.ImageId, stale: false),
        }), JsonSerializerOptions.Web);
    }

    private static async Task SeedAsync(ControlPlaneDbContext context, Guid host, DateTimeOffset now)
    {
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(host, $"t025p-{host:N}"[..20], "host-t025p", true, [EnvironmentResourceScopes.Read]), Ct);
        await new PostgresInventoryProjectionStore(context).ReplaceSnapshotAsync(new InventorySnapshot(host, 1,
            Enumerable.Range(0, Images).ToDictionary(
                i => $"{i:x64}",
                i => new ContainerInventory($"{i:x64}", $"svc-{i:D3}", Reference(i), "running", "r", now) { ImageId = Digest('a', i) })), Ct);
        Assert.Equal(ImageSnapshotOutcome.Accepted, await new PostgresImageInventoryStore(context).ReplaceSnapshotAsync(new ImageSnapshot(host, 1, now,
            Enumerable.Range(0, Images).Select(i => new ImageInventoryItem(
                Digest('a', i), [Reference(i)], [$"registry.empresa.com/app/svc-{i:D3}@{Digest('c', i)}"], 50_000_000, now.AddDays(-1), "amd64", "linux", false)).ToArray()), Ct));
    }

    private static string Reference(int i) => $"registry.empresa.com/app/svc-{i:D3}:1.0";

    private static string Digest(char prefix, int i) => $"sha256:{prefix}{i:x63}";

    private static double P95(List<double> values) => Percentile(values, 0.95);

    private static double Percentile(List<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }

    private sealed class Licensed : IImageUpdateEntitlement
    {
        public Task<bool> LicenseListsCapabilityAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
