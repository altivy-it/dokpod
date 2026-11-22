using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T004 (D-004): instantes UTC fixos e fusos IANA explícitos; nada depende do fuso do host.
/// </summary>
public sealed class ChangeWindowCalendarTests
{
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Fact]
    public void Evaluate_WeekdayNightWindow_OnTuesdayAfternoon_IsClosedUntilTuesdayNight()
    {
        // Terça, 24/11/2026, 15:00 em São Paulo (UTC-3).
        var status = ChangeWindowCalendar.Evaluate(NightWindow(), Utc(2026, 11, 24, 18, 0));

        Assert.Equal(ChangeWindowState.Closed, status.State);
        Assert.Null(status.ClosesAt);
        Assert.Equal(Utc(2026, 11, 25, 1, 0), status.NextOpensAt);
        Assert.Equal("America/Sao_Paulo", status.TimeZone);
    }

    [Fact]
    public void Evaluate_WeekdayNightWindow_OnSaturdayDawn_IsOpenByTheFridayRange()
    {
        // Sábado, 28/11/2026, 03:00 local: a faixa de sexta cruza a meia-noite e termina às 06:00.
        var status = ChangeWindowCalendar.Evaluate(NightWindow(), Utc(2026, 11, 28, 6, 0));

        Assert.Equal(ChangeWindowState.Open, status.State);
        Assert.Equal(Utc(2026, 11, 28, 9, 0), status.ClosesAt);
        Assert.Null(status.NextOpensAt);
    }

    [Fact]
    public void Evaluate_WeekdayNightWindow_OnSaturdayMorning_IsClosedUntilMondayNight()
    {
        var status = ChangeWindowCalendar.Evaluate(NightWindow(), Utc(2026, 11, 28, 10, 0));

        Assert.Equal(ChangeWindowState.Closed, status.State);
        Assert.Equal(Utc(2026, 12, 1, 1, 0), status.NextOpensAt);
    }

    [Fact]
    public void Evaluate_OverlappingRanges_CountAsTheirUnion()
    {
        var window = Window("Etc/UTC", Range([DayOfWeek.Monday], 10, 0, 12, 0), Range([DayOfWeek.Monday], 11, 0, 13, 0));

        var status = ChangeWindowCalendar.Evaluate(window, Utc(2026, 11, 23, 11, 30));

        Assert.Equal(ChangeWindowState.Open, status.State);
        Assert.Equal(Utc(2026, 11, 23, 13, 0), status.ClosesAt);
    }

    [Fact]
    public void Evaluate_ContiguousRanges_GiveASingleClosingTime()
    {
        var window = Window("Etc/UTC", Range([DayOfWeek.Monday], 22, 0, 0, 0), Range([DayOfWeek.Tuesday], 0, 0, 6, 0));

        var status = ChangeWindowCalendar.Evaluate(window, Utc(2026, 11, 23, 23, 0));

        Assert.Equal(ChangeWindowState.Open, status.State);
        Assert.Equal(Utc(2026, 11, 24, 6, 0), status.ClosesAt);
    }

    [Fact]
    public void Evaluate_StartInTheSpringGap_OpensAtTheFirstValidInstantAfterIt()
    {
        // Lisboa, domingo 29/03/2026: 01:00 vira 02:00. A faixa 01:30-03:00 começa às 02:00 locais (01:00Z).
        var window = Window("Europe/Lisbon", Range([DayOfWeek.Sunday], 1, 30, 3, 0));

        var before = ChangeWindowCalendar.Evaluate(window, Utc(2026, 3, 29, 0, 30));
        var during = ChangeWindowCalendar.Evaluate(window, Utc(2026, 3, 29, 1, 30));

        Assert.Equal(ChangeWindowState.Closed, before.State);
        Assert.Equal(Utc(2026, 3, 29, 1, 0), before.NextOpensAt);
        Assert.Equal(ChangeWindowState.Open, during.State);
        Assert.Equal(Utc(2026, 3, 29, 2, 0), during.ClosesAt);
    }

    [Fact]
    public void Evaluate_RepeatedAutumnHour_StartsAtTheFirstAndEndsAtTheSecondOccurrence()
    {
        // Lisboa, domingo 25/10/2026: 02:00 volta a 01:00. 01:30 (1ª ocorrência) = 00:30Z; 01:45 (2ª) = 01:45Z.
        var window = Window("Europe/Lisbon", Range([DayOfWeek.Sunday], 1, 30, 1, 45));

        var opened = ChangeWindowCalendar.Evaluate(window, Utc(2026, 10, 25, 0, 30));
        var repeated = ChangeWindowCalendar.Evaluate(window, Utc(2026, 10, 25, 1, 30));
        var after = ChangeWindowCalendar.Evaluate(window, Utc(2026, 10, 25, 1, 45));

        Assert.Equal(ChangeWindowState.Open, opened.State);
        Assert.Equal(Utc(2026, 10, 25, 1, 45), opened.ClosesAt);
        Assert.Equal(ChangeWindowState.Open, repeated.State);
        Assert.Equal(ChangeWindowState.Closed, after.State);
    }

    [Fact]
    public void Evaluate_HistoricalSaoPauloDaylightSaving_HandlesGapAndRepeatedHour()
    {
        // 04/11/2018 (domingo): 00:00 vira 01:00 (UTC-2). A faixa 00:30-02:00 abre às 01:00 locais (03:00Z).
        var gap = Window("America/Sao_Paulo", Range([DayOfWeek.Sunday], 0, 30, 2, 0));
        var beforeGap = ChangeWindowCalendar.Evaluate(gap, Utc(2018, 11, 4, 2, 30));
        Assert.Equal(ChangeWindowState.Closed, beforeGap.State);
        Assert.Equal(Utc(2018, 11, 4, 3, 0), beforeGap.NextOpensAt);

        // 16/02/2019 (sábado): 23:00-23:59 se repete. O fim 23:30 é a 2ª ocorrência, em UTC-3 (02:30Z do dia 17).
        var repeated = Window("America/Sao_Paulo", Range([DayOfWeek.Saturday], 22, 0, 23, 30));
        var secondPass = ChangeWindowCalendar.Evaluate(repeated, Utc(2019, 2, 17, 2, 15));
        Assert.Equal(ChangeWindowState.Open, secondPass.State);
        Assert.Equal(Utc(2019, 2, 17, 2, 30), secondPass.ClosesAt);
    }

    [Fact]
    public void Evaluate_WithoutWindow_IsNone()
    {
        var status = ChangeWindowCalendar.Evaluate(null, Utc(2026, 11, 24, 18, 0));

        Assert.Equal(ChangeWindowState.None, status.State);
        Assert.Null(status.ClosesAt);
        Assert.Null(status.NextOpensAt);
        Assert.Null(status.TimeZone);
    }

    [Fact]
    public void Evaluate_WeeklyRange_FindsTheNextOpeningAlmostAWeekAhead()
    {
        var window = Window("Etc/UTC", Range([DayOfWeek.Monday], 10, 0, 11, 0));

        var status = ChangeWindowCalendar.Evaluate(window, Utc(2026, 11, 23, 11, 30));

        Assert.Equal(ChangeWindowState.Closed, status.State);
        Assert.Equal(Utc(2026, 11, 30, 10, 0), status.NextOpensAt);
    }

    [Fact]
    public void Evaluate_RangeEnd_IsExclusive()
    {
        var window = Window("Etc/UTC", Range([DayOfWeek.Monday], 10, 0, 11, 0));

        Assert.Equal(ChangeWindowState.Open, ChangeWindowCalendar.Evaluate(window, Utc(2026, 11, 23, 10, 0)).State);
        Assert.Equal(ChangeWindowState.Closed, ChangeWindowCalendar.Evaluate(window, Utc(2026, 11, 23, 11, 0)).State);
    }

    [Fact]
    public void Validate_RejectsInvalidFields()
    {
        var errors = ChangeWindow.Validate(
            "Mars/Olympus",
            [Range([], 10, 0, 10, 0), Range([DayOfWeek.Monday], 10, 0, 11, 0) with { Start = new TimeOnly(10, 0, 30) }]);

        Assert.Contains(errors, error => error.Field == "timeZone");
        Assert.Contains(errors, error => error.Field == "ranges[0].days");
        Assert.Contains(errors, error => error.Field == "ranges[0].end");
        Assert.Contains(errors, error => error.Field == "ranges[1].start");
        Assert.Contains(ChangeWindow.Validate("Etc/UTC", []), error => error.Field == "ranges");
        Assert.Contains(
            ChangeWindow.Validate("Etc/UTC", Enumerable.Repeat(Range([DayOfWeek.Monday], 1, 0, 2, 0), 15).ToArray()),
            error => error.Field == "ranges");
        Assert.Empty(ChangeWindow.Validate("America/Sao_Paulo", NightWindow().Ranges));
    }

    private static ChangeWindow NightWindow() => Window("America/Sao_Paulo", Range(Weekdays, 22, 0, 6, 0));

    private static ChangeWindow Window(string timeZone, params ChangeWindowRange[] ranges) =>
        new(EnvironmentStage.Production, timeZone, ranges, 1, "Ana", Utc(2026, 11, 20, 18, 0));

    private static ChangeWindowRange Range(DayOfWeek[] days, int startHour, int startMinute, int endHour, int endMinute) =>
        new(new HashSet<DayOfWeek>(days), new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
