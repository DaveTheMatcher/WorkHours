using WorkHours.Core.Models;

namespace WorkHours.Core.Services;

/// <summary>Riepilogo di un mese.</summary>
/// <param name="Target">Obiettivo dell'intero mese.</param>
/// <param name="ExpectedToDate">Obiettivo maturato fino alla data di riferimento (inclusa).</param>
/// <param name="Worked">Ore lavorate fino alla data di riferimento (inclusa).</param>
public sealed record MonthSummary(int Year, int Month, TimeSpan Target, TimeSpan ExpectedToDate, TimeSpan Worked)
{
    /// <summary>Positivo = in eccesso, negativo = in difetto.</summary>
    public TimeSpan Balance => Worked - ExpectedToDate;

    /// <summary>Ore che mancano per chiudere il mese in pari (0 se già raggiunto).</summary>
    public TimeSpan RemainingToTarget => Target > Worked ? Target - Worked : TimeSpan.Zero;
}

/// <summary>Riepilogo di una settimana, limitata ai giorni del mese a cui appartiene.</summary>
/// <param name="CarryIn">Saldo del mese accumulato prima dell'inizio della settimana.</param>
public sealed record WeekSummary(DateOnly From, DateOnly To, TimeSpan Target, TimeSpan CarryIn, TimeSpan Worked)
{
    /// <summary>Ore da fare in settimana per tornare in pari: obiettivo meno il saldo precedente.</summary>
    public TimeSpan NeededThisWeek => Target - CarryIn;

    /// <summary>Quante ne mancano ancora (0 se già fatte).</summary>
    public TimeSpan Remaining => NeededThisWeek > Worked ? NeededThisWeek - Worked : TimeSpan.Zero;
}

/// <summary>
/// Regole di calcolo (tutte qui, così si cambiano in un punto solo):
/// - ogni giorno lavorativo vale (ore settimanali / giorni lavorativi), es. 20 h / 5 = 4 h;
/// - ferie, malattia, permesso e festivi valgono 0 (coprono il giorno);
/// - i giorni prima della prima registrazione valgono 0 (l'app non era ancora in uso);
/// - il saldo si accumula giorno per giorno e si azzera a ogni inizio mese;
/// - la settimana va da lunedì a domenica, tagliata ai confini del mese.
/// </summary>
public sealed class BalanceCalculator
{
    private readonly Settings _settings;
    private readonly Dictionary<DateOnly, WorkDay> _days;
    private readonly Dictionary<int, IReadOnlyDictionary<DateOnly, string>> _holidayCache = new();

    public BalanceCalculator(Settings settings, IEnumerable<WorkDay> days)
    {
        _settings = settings;
        _days = days.ToDictionary(d => d.Date);
        TrackingStart = _days.Count > 0 ? _days.Keys.Min() : null;
    }

    /// <summary>
    /// Primo giorno registrato: prima di questa data l'app non era in uso,
    /// quindi quei giorni non contano nell'obiettivo (niente debito iniziale finto).
    /// </summary>
    public DateOnly? TrackingStart { get; }

    public string? HolidayName(DateOnly date)
    {
        if (!_holidayCache.TryGetValue(date.Year, out var holidays))
            _holidayCache[date.Year] = holidays = ItalianHolidays.For(date.Year);
        return holidays.GetValueOrDefault(date);
    }

    /// <summary>Tipo effettivo del giorno: quello scelto a mano, altrimenti Festivo/Lavoro in automatico.</summary>
    public DayType EffectiveType(DateOnly date) =>
        _days.GetValueOrDefault(date)?.Type ?? (HolidayName(date) is not null ? DayType.Festivo : DayType.Lavoro);

    public TimeSpan Expected(DateOnly date)
    {
        if (TrackingStart is null || date < TrackingStart)
            return TimeSpan.Zero;

        var schedule = _settings.ScheduleFor(date);
        if (!schedule.IsWorkingDay(date) || EffectiveType(date).CoversTarget())
            return TimeSpan.Zero;
        return schedule.DailyHours;
    }

    public TimeSpan Worked(DateOnly date) => _days.GetValueOrDefault(date)?.Worked ?? TimeSpan.Zero;

    public MonthSummary Month(int year, int month, DateOnly asOf)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var upTo = asOf < last ? asOf : last;

        TimeSpan target = TimeSpan.Zero, expected = TimeSpan.Zero, worked = TimeSpan.Zero;
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var e = Expected(d);
            target += e;
            if (d <= upTo)
            {
                expected += e;
                worked += Worked(d);
            }
        }
        return new MonthSummary(year, month, target, expected, worked);
    }

    public WeekSummary Week(DateOnly date)
    {
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var from = monday < monthStart ? monthStart : monday;
        var to = monday.AddDays(6) > monthEnd ? monthEnd : monday.AddDays(6);

        var carry = TimeSpan.Zero;
        for (var d = monthStart; d < from; d = d.AddDays(1))
            carry += Worked(d) - Expected(d);

        TimeSpan target = TimeSpan.Zero, worked = TimeSpan.Zero;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            target += Expected(d);
            worked += Worked(d);
        }
        return new WeekSummary(from, to, target, carry, worked);
    }
}
