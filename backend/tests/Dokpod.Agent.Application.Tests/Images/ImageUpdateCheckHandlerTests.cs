using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Images;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Images;

/// <summary>Spec 025 T011: um resultado por item, na ordem, em páginas de até 256 KiB, com os limites de D-005 e logs L2.</summary>
public sealed class ImageUpdateCheckHandlerTests
{
    private static readonly string Digest = "sha256:" + new string('a', 64);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryItemGetsOneResult_InOrder_OnASingleLastPage()
    {
        var registry = new SlowRegistry(TimeSpan.Zero, new ManifestHead(Digest, ManifestMediaTypes.OciManifest, null));
        var pages = new List<ImageUpdateCheckResultPage>();

        await Handler(registry).HandleAsync(Request("nginx:1.28", "app@" + Digest, "redis:7"), Collect(pages), Ct);

        var page = Assert.Single(pages);
        Assert.True(page.IsLastPage);
        Assert.Equal(["nginx:1.28", "app@" + Digest, "redis:7"], page.Results.Select(r => r.Reference));
        Assert.Equal([ImageUpdateState.UpToDate, ImageUpdateState.NotCheckable, ImageUpdateState.UpToDate], page.Results.Select(r => r.State));
        Assert.All(page.Results, r => Assert.NotNull(r.CheckedAt));
    }

    [Fact]
    public async Task ManyResults_AreSplitInPagesWithinTheLimit()
    {
        var registry = new SlowRegistry(TimeSpan.Zero, new ManifestHead(Digest, ManifestMediaTypes.OciManifest, null));
        var references = Enumerable.Range(0, 500).Select(i => $"registro.exemplo.com/{new string('r', 200)}/app{i}:1.0").ToArray();
        var pages = new List<ImageUpdateCheckResultPage>();

        // 500 resultados cabem em 256 KiB; o limite menor exercita a paginação com o mesmo código.
        const int limit = 16 * 1024;
        await new ImageUpdateCheckHandler(new Engine(), () => registry, TimeProvider.System, maxPageBytes: limit).HandleAsync(Request(references), Collect(pages), Ct);

        Assert.True(pages.Count > 1);
        Assert.All(pages, p => Assert.True(p.CalculateSize() <= limit));
        Assert.Equal(Enumerable.Range(0, pages.Count).Select(i => (uint)i), pages.Select(p => p.PageNumber));
        Assert.Equal([.. Enumerable.Repeat(false, pages.Count - 1), true], pages.Select(p => p.IsLastPage));
        Assert.Equal(references, pages.SelectMany(p => p.Results).Select(r => r.Reference));
    }

    [Fact]
    public async Task ItemsAbove500_AreAnsweredAsFailed_AndConcurrencyIsAtMostFour()
    {
        var registry = new SlowRegistry(TimeSpan.FromMilliseconds(5), new ManifestHead(Digest, ManifestMediaTypes.OciManifest, null));
        var pages = new List<ImageUpdateCheckResultPage>();

        await Handler(registry).HandleAsync(Request(Enumerable.Range(0, 502).Select(i => $"app{i}:1").ToArray()), Collect(pages), Ct);

        var results = pages.SelectMany(p => p.Results).ToArray();
        Assert.Equal(502, results.Length);
        Assert.All(results[500..], r => Assert.Equal((ImageUpdateState.Failed, ImageUpdateReasons.InvalidResponse), (r.State, r.Reason)));
        Assert.Equal(500, registry.Calls);
        Assert.InRange(registry.MaxConcurrent, 1, ImageUpdateCheckHandler.MaxConcurrency);
    }

    [Fact]
    public async Task Deadline_TurnsTheRemainingItemsIntoTimeout_AndStillAnswers()
    {
        var registry = new SlowRegistry(TimeSpan.FromSeconds(30), new ManifestHead(Digest, ManifestMediaTypes.OciManifest, null));
        var pages = new List<ImageUpdateCheckResultPage>();

        await Handler(registry, deadline: TimeSpan.FromMilliseconds(200)).HandleAsync(Request("nginx:1.28", "redis:7"), Collect(pages), Ct);

        var page = Assert.Single(pages);
        Assert.All(page.Results, r => Assert.Equal((ImageUpdateState.Failed, ImageUpdateReasons.Timeout), (r.State, r.Reason)));
    }

    [Fact]
    public async Task UnexpectedFailure_IsLoggedOnlyByTypeAndReason()
    {
        var registry = new SlowRegistry(TimeSpan.Zero, null) { Throw = new InvalidOperationException("token=segredo https://usuario:senha@registro") };
        var failures = new List<(string Type, string Reason)>();
        var pages = new List<ImageUpdateCheckResultPage>();

        await Handler(registry, onFailure: (type, reason) => failures.Add((type, reason))).HandleAsync(Request("nginx:1.28"), Collect(pages), Ct);

        Assert.Equal([("InvalidOperationException", ImageUpdateReasons.RegistryUnavailable)], failures);
        var result = Assert.Single(Assert.Single(pages).Results);
        Assert.Equal((ImageUpdateState.Failed, ImageUpdateReasons.RegistryUnavailable), (result.State, result.Reason));
        Assert.DoesNotContain("segredo", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachRequestGetsANewRegistryClient()
    {
        var created = 0;
        var handler = new ImageUpdateCheckHandler(new Engine(), () =>
        {
            created++;
            return new SlowRegistry(TimeSpan.Zero, new ManifestHead(Digest, ManifestMediaTypes.OciManifest, null));
        }, TimeProvider.System);

        await handler.HandleAsync(Request("nginx:1.28"), _ => Task.CompletedTask, Ct);
        await handler.HandleAsync(Request("nginx:1.28"), _ => Task.CompletedTask, Ct);

        Assert.Equal(2, created);
    }

    private static ImageUpdateCheckHandler Handler(SlowRegistry registry, TimeSpan? deadline = null, Action<string, string>? onFailure = null) =>
        new(new Engine(), () => registry, TimeProvider.System, onFailure, deadline);

    private static ImageUpdateCheckRequest Request(params string[] references)
    {
        var request = new ImageUpdateCheckRequest { RequestId = Guid.NewGuid().ToString("D") };
        request.Items.AddRange(references.Select(r => new ImageUpdateCheckItem { Reference = r, ImageId = "sha256:" + new string('f', 64) }));
        return request;
    }

    private static Func<AgentMessage, Task> Collect(List<ImageUpdateCheckResultPage> pages) => message =>
    {
        lock (pages) pages.Add(message.ImageUpdateCheckResult);
        return Task.CompletedTask;
    };

    private sealed class SlowRegistry(TimeSpan delay, ManifestHead? head) : IRegistryManifestClient
    {
        private int running;

        public Exception? Throw { get; init; }

        public int Calls;

        public int MaxConcurrent;

        public async Task<ManifestHead> HeadAsync(ImageReference reference, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref MaxConcurrent, now);
            try
            {
                if (Throw is not null) throw Throw;
                await Task.Delay(delay, cancellationToken);
                return head!;
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }

        public Task<ManifestIndex> GetIndexAsync(ImageReference reference, string digest, CancellationToken cancellationToken) =>
            Task.FromResult(ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse));

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }

    /// <summary>Toda imagem local tem o digest do registro, para os itens chegarem à consulta.</summary>
    private sealed class Engine : IContainerEngine
    {
        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("1", "1.47"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EngineContainer>>([]);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(Dokpod.Domain.Commands.AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerMutationResult(false, "x"));

        // RepoDigests "curinga": o checker só compara pelo nome, então devolvemos um digest para cada repositório pedido.
        public Task<EngineImagePlatform?> InspectImagePlatformAsync(string imageId, CancellationToken cancellationToken) =>
            Task.FromResult<EngineImagePlatform?>(new EngineImagePlatform("linux", "amd64", null, Repositories));

        private static readonly string[] Repositories = BuildRepositories();

        private static string[] BuildRepositories()
        {
            var names = new List<string> { $"docker.io/library/nginx@{Digest}", $"docker.io/library/redis@{Digest}" };
            names.AddRange(Enumerable.Range(0, 502).Select(i => $"docker.io/library/app{i}@{Digest}"));
            names.AddRange(Enumerable.Range(0, 500).Select(i => $"registro.exemplo.com/{new string('r', 200)}/app{i}@{Digest}"));
            return [.. names];
        }
    }
}
