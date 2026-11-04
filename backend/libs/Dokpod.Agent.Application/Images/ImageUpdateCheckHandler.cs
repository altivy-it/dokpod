using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Images;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Images;

/// <summary>
/// Atende a mensagem 17 (spec 025 T011, D-004 e D-005): até 500 itens, 4 consultas simultâneas e 5 min por pedido;
/// responde em páginas de até 256 KiB, sempre com um resultado por item e na ordem dos itens, inclusive quando tudo
/// falha. O cliente do registro é criado por pedido, então o token de cada repositório não sobrevive a ele (L3).
/// Falhas inesperadas só chegam a <paramref name="onFailure"/> com o tipo da exceção e o motivo, nunca com mensagem,
/// URL ou referência (L2).
/// </summary>
public sealed class ImageUpdateCheckHandler(
    IContainerEngine engine,
    Func<IRegistryManifestClient> createRegistryClient,
    TimeProvider timeProvider,
    Action<string, string>? onFailure = null,
    TimeSpan? requestDeadline = null,
    int? maxPageBytes = null)
{
    public const int MaxItems = 500;
    public const int MaxConcurrency = 4;
    public const int MaxPageBytes = 256 * 1024;
    public static readonly TimeSpan RequestDeadline = TimeSpan.FromMinutes(5);

    public async Task HandleAsync(ImageUpdateCheckRequest request, Func<AgentMessage, Task> send, CancellationToken cancellationToken)
    {
        var items = request.Items;
        var outcomes = new ImageUpdateCheckOutcome?[items.Count];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(requestDeadline ?? RequestDeadline);
        try
        {
            var registry = createRegistryClient();
            var config = await engine.GetRegistryConfigAsync(deadline.Token);
            var insecure = config.Registries.Where(r => r.Insecure).Select(r => r.Registry).ToHashSet(StringComparer.Ordinal);
            var checker = new ImageUpdateChecker(engine, registry, insecure);
            await Parallel.ForEachAsync(
                Enumerable.Range(0, Math.Min(items.Count, MaxItems)),
                new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = deadline.Token },
                async (index, token) => outcomes[index] = await CheckOneAsync(checker, items[index], token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Prazo do pedido esgotado: o que faltou vira `timeout` abaixo.
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            onFailure?.Invoke(exception.GetType().Name, ImageUpdateReasons.RegistryUnavailable);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow());
        var results = items.Select((item, index) => ToResult(
            index >= MaxItems
                ? Failed(item, ImageUpdateReasons.InvalidResponse)
                : outcomes[index] ?? Failed(item, ImageUpdateReasons.Timeout), now));
        await SendPagesAsync(request.RequestId, results, send, maxPageBytes ?? MaxPageBytes);
    }

    private async Task<ImageUpdateCheckOutcome> CheckOneAsync(ImageUpdateChecker checker, ImageUpdateCheckItem item, CancellationToken cancellationToken)
    {
        try
        {
            return await checker.CheckAsync(new ImageUpdateCheckInput(item.Reference, item.ImageId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            onFailure?.Invoke(exception.GetType().Name, ImageUpdateReasons.RegistryUnavailable);
            return Failed(item, ImageUpdateReasons.RegistryUnavailable);
        }
    }

    private static ImageUpdateCheckOutcome Failed(ImageUpdateCheckItem item, string reason) =>
        new(item.Reference, ImageUpdateChecker.HostPlatform(), ImageUpdateStates.Failed, reason, null, null);

    private static ImageUpdateCheckResult ToResult(ImageUpdateCheckOutcome outcome, Timestamp checkedAt) => new()
    {
        Reference = outcome.Reference,
        Platform = outcome.Platform,
        State = outcome.State switch
        {
            ImageUpdateStates.UpToDate => ImageUpdateState.UpToDate,
            ImageUpdateStates.UpdateAvailable => ImageUpdateState.UpdateAvailable,
            ImageUpdateStates.NotCheckable => ImageUpdateState.NotCheckable,
            _ => ImageUpdateState.Failed,
        },
        Reason = outcome.Reason ?? string.Empty,
        RemoteDigest = outcome.RemoteDigest ?? string.Empty,
        MatchedLocalDigest = outcome.MatchedLocalDigest ?? string.Empty,
        CheckedAt = checkedAt,
    };

    private static async Task SendPagesAsync(string requestId, IEnumerable<ImageUpdateCheckResult> results, Func<AgentMessage, Task> send, int pageLimit)
    {
        var page = new ImageUpdateCheckResultPage { RequestId = requestId, PageNumber = 0 };
        foreach (var result in results)
        {
            if (page.Results.Count > 0 && page.CalculateSize() + result.CalculateSize() + 8 > pageLimit)
            {
                await send(new AgentMessage { ImageUpdateCheckResult = page });
                page = new ImageUpdateCheckResultPage { RequestId = requestId, PageNumber = page.PageNumber + 1 };
            }

            page.Results.Add(result);
        }

        page.IsLastPage = true;
        await send(new AgentMessage { ImageUpdateCheckResult = page });
    }
}
