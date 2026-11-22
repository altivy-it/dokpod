using Dokpod.Domain.ChangeWindows;

namespace Dokpod.ControlPlane.Application.ChangeWindows;

public enum ChangeWindowState
{
    /// <summary>Estágio sem janela, host sem estágio ou CE: nada fica fechado.</summary>
    None,
    Open,
    Closed,
}

/// <summary>Estado calculado de uma janela num instante (spec 026 data-model.md, "Estado calculado").</summary>
public sealed record ChangeWindowStatus(
    ChangeWindowState State,
    DateTimeOffset? ClosesAt,
    DateTimeOffset? NextOpensAt,
    string? TimeZone)
{
    public static ChangeWindowStatus None { get; } = new(ChangeWindowState.None, null, null, null);
}

/// <summary>
/// Função pura da janela (spec 026 D-004): cada faixa vira um intervalo UTC em cada dia local, as sobrepostas e as
/// contíguas se unem, e o instante cai dentro de um intervalo (aberta, com o fim da união) ou fora (fechada, com a
/// próxima abertura nos 8 dias locais seguintes). Horário de verão: um horário no buraco vale como o primeiro instante
/// válido depois dele; um horário repetido vale a primeira ocorrência no início e a segunda no fim.
/// </summary>
public static class ChangeWindowCalendar
{
    private const int SearchDays = 8;

    public static ChangeWindowStatus Evaluate(ChangeWindow? window, DateTimeOffset utc)
    {
        if (window is null || !ChangeWindow.TryFindTimeZone(window.TimeZone, out var zone)) return ChangeWindowStatus.None;
        var instant = utc.ToUniversalTime();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

        var intervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        // Um dia antes, para a faixa que começou ontem e cruza a meia-noite.
        for (var offset = -1; offset <= SearchDays; offset++)
        {
            var date = today.AddDays(offset);
            foreach (var range in window.Ranges)
            {
                if (!range.Days.Contains(date.DayOfWeek)) continue;
                var start = ToUtc(date.ToDateTime(range.Start), zone, firstOccurrence: true);
                var end = ToUtc((range.CrossesMidnight ? date.AddDays(1) : date).ToDateTime(range.End), zone, firstOccurrence: false);
                if (end > start) intervals.Add((start, end));
            }
        }

        intervals.Sort((left, right) => left.Start.CompareTo(right.Start));
        DateTimeOffset? nextOpensAt = null;
        for (var i = 0; i < intervals.Count; i++)
        {
            var (start, end) = intervals[i];
            while (i + 1 < intervals.Count && intervals[i + 1].Start <= end)
            {
                i++;
                if (intervals[i].End > end) end = intervals[i].End;
            }

            if (start <= instant && instant < end) return new ChangeWindowStatus(ChangeWindowState.Open, end, null, window.TimeZone);
            if (start > instant && nextOpensAt is null) nextOpensAt = start;
        }

        return new ChangeWindowStatus(ChangeWindowState.Closed, null, nextOpensAt, window.TimeZone);
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone, bool firstOccurrence)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // No buraco da mudança de horário, o primeiro minuto válido depois dele (o buraco não passa de algumas horas).
        for (var guard = 0; zone.IsInvalidTime(local) && guard < 24 * 60; guard++) local = local.AddMinutes(1);
        TimeSpan utcOffset;
        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            // A primeira ocorrência tem o maior deslocamento (o instante UTC mais cedo).
            utcOffset = firstOccurrence ? offsets.Max() : offsets.Min();
        }
        else
        {
            utcOffset = zone.GetUtcOffset(local);
        }

        return new DateTimeOffset(local.Ticks - utcOffset.Ticks, TimeSpan.Zero);
    }
}
