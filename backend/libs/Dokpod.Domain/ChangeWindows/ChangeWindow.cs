using Dokpod.Domain.Environments;

namespace Dokpod.Domain.ChangeWindows;

/// <summary>
/// Faixa semanal de horário em que as ações que mudam o host são aceitas (spec 026 data-model.md). Os dias são os de
/// início; com <see cref="End"/> menor que <see cref="Start"/>, a faixa cruza a meia-noite e pertence ao dia de início.
/// </summary>
public sealed record ChangeWindowRange(IReadOnlySet<DayOfWeek> Days, TimeOnly Start, TimeOnly End)
{
    public bool CrossesMidnight => End < Start;
}

/// <summary>Erro de validação de uma janela, com o campo do corpo do <c>PUT</c> (<c>400 change_window_invalid</c>).</summary>
public sealed record ChangeWindowFieldError(string Field, string Code);

/// <summary>Janela de mudança de um estágio: um fuso IANA e de 1 a 14 faixas (spec 026 D-003, D-004).</summary>
public sealed record ChangeWindow(
    EnvironmentStage Stage,
    string TimeZone,
    IReadOnlyList<ChangeWindowRange> Ranges,
    int Version,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc)
{
    public const int MaxTimeZoneLength = 64;
    public const int MaxRanges = 14;

    /// <summary>Valida fuso e faixas sem lançar exceção; lista vazia quando a janela é válida.</summary>
    public static IReadOnlyList<ChangeWindowFieldError> Validate(string? timeZone, IReadOnlyList<ChangeWindowRange>? ranges)
    {
        var errors = new List<ChangeWindowFieldError>();
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > MaxTimeZoneLength || !TryFindTimeZone(timeZone, out _))
        {
            errors.Add(new ChangeWindowFieldError("timeZone", "invalid"));
        }

        if (ranges is null || ranges.Count is 0 or > MaxRanges)
        {
            errors.Add(new ChangeWindowFieldError("ranges", "count"));
            return errors;
        }

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            if (range is null)
            {
                errors.Add(new ChangeWindowFieldError($"ranges[{i}]", "required"));
                continue;
            }

            if (range.Days is null || range.Days.Count is 0 or > 7 || range.Days.Any(day => !Enum.IsDefined(day)))
            {
                errors.Add(new ChangeWindowFieldError($"ranges[{i}].days", "invalid"));
            }

            if (!IsWholeMinute(range.Start)) errors.Add(new ChangeWindowFieldError($"ranges[{i}].start", "invalid"));
            if (!IsWholeMinute(range.End) || range.End == range.Start) errors.Add(new ChangeWindowFieldError($"ranges[{i}].end", "invalid"));
        }

        return errors;
    }

    /// <summary>Fuso IANA da imagem (<c>tzdata</c>); nunca o deslocamento fixo nem o fuso local do host.</summary>
    public static bool TryFindTimeZone(string timeZone, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > MaxTimeZoneLength || timeZone.Any(char.IsControl)) return false;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found)) return false;
        zone = found;
        return true;
    }

    private static bool IsWholeMinute(TimeOnly value) => value.Second == 0 && value.Millisecond == 0 && value.Microsecond == 0 && value.Nanosecond == 0;
}

/// <summary>Dias no formato do contrato (<c>mon</c>…<c>sun</c>) e horas em <c>HH:mm</c>.</summary>
public static class ChangeWindowFormat
{
    private static readonly string[] DayValues = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    public static string DayToValue(DayOfWeek day) => DayValues[(int)day];

    public static bool TryParseDay(string? value, out DayOfWeek day)
    {
        var index = Array.IndexOf(DayValues, value);
        day = index < 0 ? default : (DayOfWeek)index;
        return index >= 0;
    }

    public static string TimeToValue(TimeOnly time) => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out time);
}
