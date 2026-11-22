using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure.ChangeWindows;

/// <summary>
/// Janelas de mudança por estágio (spec 026 data-model.md, D-003). Leitura por chave primária a cada decisão, sem cache
/// (D-011); criação só sem linha e edição e remoção só na versão esperada, com o conflito como resultado.
/// </summary>
public sealed class ChangeWindowStore(ControlPlaneDbContext dbContext) : IChangeWindowSchedule, IChangeWindowStore
{
    private const string Select = "SELECT stage, time_zone, ranges::text, version, updated_by, updated_at_utc FROM dokpod.change_windows";

    public async Task<ChangeWindow?> GetAsync(EnvironmentStage stage, CancellationToken cancellationToken) =>
        (await QueryAsync($"{Select} WHERE stage = @stage", [P("stage", EnvironmentStages.ToValue(stage))], cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

    public async Task<IReadOnlyList<ChangeWindow>> ListAsync(CancellationToken cancellationToken) =>
        await QueryAsync($"{Select} ORDER BY stage", [], cancellationToken).ConfigureAwait(false);

    public async Task<ChangeWindowWriteResult> SaveAsync(ChangeWindow window, int? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (ChangeWindow.Validate(window.TimeZone, window.Ranges).Count > 0) throw new ArgumentException("Change window is invalid.", nameof(window));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var stage = EnvironmentStages.ToValue(window.Stage);
        var previous = (await QueryAsync($"{Select} WHERE stage = @stage FOR UPDATE", [P("stage", stage)], cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();
        if (expectedVersion is null ? previous is not null : previous?.Version != expectedVersion)
        {
            return new ChangeWindowWriteResult(previous is null && expectedVersion is not null ? ChangeWindowWriteOutcome.NotFound : ChangeWindowWriteOutcome.VersionConflict, null, previous);
        }

        var saved = window with { Version = (previous?.Version ?? 0) + 1 };
        IReadOnlyList<NpgsqlParameter> parameters =
        [
            P("stage", stage),
            P("zone", saved.TimeZone),
            new NpgsqlParameter("ranges", NpgsqlDbType.Jsonb) { Value = SerializeRanges(saved.Ranges) },
            P("version", saved.Version),
            P("by", saved.UpdatedBy.Length <= 255 ? saved.UpdatedBy : saved.UpdatedBy[..255]),
            P("at", saved.UpdatedAtUtc),
            P("expected", expectedVersion ?? 0),
        ];
        var written = previous is null
            ? await ExecuteAsync(
                """
                INSERT INTO dokpod.change_windows (stage, time_zone, ranges, version, updated_by, updated_at_utc)
                VALUES (@stage, @zone, @ranges, @version, @by, @at)
                """,
                parameters,
                cancellationToken).ConfigureAwait(false)
            : await ExecuteAsync(
                """
                UPDATE dokpod.change_windows
                SET time_zone = @zone, ranges = @ranges, version = @version, updated_by = @by, updated_at_utc = @at
                WHERE stage = @stage AND version = @expected
                """,
                parameters,
                cancellationToken).ConfigureAwait(false);
        if (written != 1) return new ChangeWindowWriteResult(ChangeWindowWriteOutcome.VersionConflict, null, previous);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ChangeWindowWriteResult(ChangeWindowWriteOutcome.Saved, saved, previous);
    }

    public async Task<ChangeWindowWriteResult> DeleteAsync(EnvironmentStage stage, int expectedVersion, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var previous = (await QueryAsync($"{Select} WHERE stage = @stage FOR UPDATE", [P("stage", EnvironmentStages.ToValue(stage))], cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();
        if (previous is null) return new ChangeWindowWriteResult(ChangeWindowWriteOutcome.NotFound, null, null);
        if (previous.Version != expectedVersion) return new ChangeWindowWriteResult(ChangeWindowWriteOutcome.VersionConflict, null, previous);
        var deleted = await ExecuteAsync(
            "DELETE FROM dokpod.change_windows WHERE stage = @stage AND version = @expected",
            [P("stage", EnvironmentStages.ToValue(stage)), P("expected", expectedVersion)],
            cancellationToken).ConfigureAwait(false);
        if (deleted != 1) return new ChangeWindowWriteResult(ChangeWindowWriteOutcome.VersionConflict, null, previous);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ChangeWindowWriteResult(ChangeWindowWriteOutcome.Removed, null, previous);
    }

    /// <summary>Faixas no formato do contrato: <c>{"days":["mon"],"start":"22:00","end":"06:00"}</c>.</summary>
    internal static string SerializeRanges(IReadOnlyList<ChangeWindowRange> ranges) =>
        JsonSerializer.Serialize(
            ranges.Select(range => new StoredRange(
                range.Days.OrderBy(day => ((int)day + 6) % 7).Select(ChangeWindowFormat.DayToValue).ToArray(),
                ChangeWindowFormat.TimeToValue(range.Start),
                ChangeWindowFormat.TimeToValue(range.End))).ToArray(),
            StoredRangeJson.Default.StoredRangeArray);

    internal static IReadOnlyList<ChangeWindowRange> DeserializeRanges(string json) =>
        (JsonSerializer.Deserialize(json, StoredRangeJson.Default.StoredRangeArray) ?? [])
            .Select(stored => new ChangeWindowRange(
                stored.Days.Select(value => ChangeWindowFormat.TryParseDay(value, out var day) ? day : throw new InvalidDataException("Stored change window day is invalid.")).ToHashSet(),
                ChangeWindowFormat.TryParseTime(stored.Start, out var start) ? start : throw new InvalidDataException("Stored change window start is invalid."),
                ChangeWindowFormat.TryParseTime(stored.End, out var end) ? end : throw new InvalidDataException("Stored change window end is invalid.")))
            .ToArray();

    private static ChangeWindow Read(DbDataReader reader)
    {
        if (!EnvironmentStages.TryParse(reader.GetString(0), out var stage)) throw new InvalidDataException("Stored change window stage is invalid.");
        return new ChangeWindow(
            stage,
            reader.GetString(1),
            DeserializeRanges(reader.GetString(2)),
            reader.GetInt32(3),
            reader.GetString(4),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
    }

    private static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private async Task<int> ExecuteAsync(string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(sql, parameters.Select(parameter => parameter.Clone()), cancellationToken).ConfigureAwait(false);

    private async Task<List<ChangeWindow>> QueryAsync(string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var parameter in parameters) command.Parameters.Add(parameter.Clone());
            var results = new List<ChangeWindow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(Read(reader));
            return results;
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}

internal sealed record StoredRange(
    [property: JsonPropertyName("days")] string[] Days,
    [property: JsonPropertyName("start")] string Start,
    [property: JsonPropertyName("end")] string End);

[JsonSerializable(typeof(StoredRange[]))]
internal sealed partial class StoredRangeJson : JsonSerializerContext;
