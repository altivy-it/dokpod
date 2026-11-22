using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.ChangeWindows;

/// <summary>
/// Calendário das janelas por estágio (spec 026 D-002). A CE registra <see cref="NoChangeWindows"/>; o módulo Business
/// o substitui pelo store sobre o PostgreSQL. O guard só depende desta porta, nunca do gate da licença (FR-007).
/// </summary>
public interface IChangeWindowSchedule
{
    Task<ChangeWindow?> GetAsync(EnvironmentStage stage, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChangeWindow>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>Calendário da CE: nenhum estágio tem janela, então nada fica fechado e nada é consultado.</summary>
public sealed class NoChangeWindows : IChangeWindowSchedule
{
    public Task<ChangeWindow?> GetAsync(EnvironmentStage stage, CancellationToken cancellationToken) =>
        Task.FromResult<ChangeWindow?>(null);

    public Task<IReadOnlyList<ChangeWindow>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ChangeWindow>>([]);
}

public enum ChangeWindowWriteOutcome
{
    Saved,
    Removed,
    /// <summary>A versão esperada não bate com a gravada, ou a janela já existe na criação (<c>412</c>).</summary>
    VersionConflict,
    NotFound,
}

public sealed record ChangeWindowWriteResult(ChangeWindowWriteOutcome Outcome, ChangeWindow? Window, ChangeWindow? Previous);

/// <summary>Gravação das janelas (só Business). O conflito de versão é resultado, não exceção.</summary>
public interface IChangeWindowStore
{
    /// <summary>Com <paramref name="expectedVersion"/> nulo, só cria; com versão, só atualiza aquela versão.</summary>
    Task<ChangeWindowWriteResult> SaveAsync(ChangeWindow window, int? expectedVersion, CancellationToken cancellationToken);

    Task<ChangeWindowWriteResult> DeleteAsync(EnvironmentStage stage, int expectedVersion, CancellationToken cancellationToken);
}
