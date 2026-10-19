using WorkHours.Core.Models;
using WorkHours.Core.Services;

namespace WorkHours.Core.Tests;

public class BalanceCalculatorTests
{
    private static readonly Settings DefaultSettings = new();

    // Giorno di "inizio uso" dell'app: senza registrazioni l'obiettivo è 0.
    private static readonly WorkDay Start = new() { Date = new DateOnly(2026, 1, 1), Note = "inizio" };

    private static WorkDay Day(int y, int m, int d, double hours) => new()
    {
        Date = new DateOnly(y, m, d),
        Slots = [new TimeSlot(new TimeOnly(9, 0), new TimeOnly(9, 0).Add(TimeSpan.FromHours(hours)))],
    };

    [Fact]
    public void Ottobre2026_ObiettivoE92Ore()
    {
        // 23 giorni da martedì a sabato, nessun festivo lavorativo (il 4/10 è domenica).
        var calc = new BalanceCalculator(DefaultSettings, [Start]);
        Assert.Equal(TimeSpan.FromHours(92), calc.Month(2026, 10, new DateOnly(2026, 10, 31)).Target);
    }

    [Fact]
    public void Dicembre2026_FestiviTolgonoOre()
    {
        // 23 giorni mar–sab, meno 8, 25 e 26 dicembre = 20 giorni = 80 h.
        var calc = new BalanceCalculator(DefaultSettings, [Start]);
        Assert.Equal(TimeSpan.FromHours(80), calc.Month(2026, 12, new DateOnly(2026, 12, 31)).Target);
    }

    [Fact]
    public void FestivoTolto_TornaAValere4Ore()
    {
        var days = new[] { new WorkDay { Date = new DateOnly(2026, 12, 8), Type = DayType.Lavoro } };
        var calc = new BalanceCalculator(DefaultSettings, days.Append(Start));
        Assert.Equal(TimeSpan.FromHours(84), calc.Month(2026, 12, new DateOnly(2026, 12, 31)).Target);
    }

    [Fact]
    public void Ferie_CopronoIlGiorno()
    {
        var days = new[] { new WorkDay { Date = new DateOnly(2026, 10, 6), Type = DayType.Ferie } };
        var calc = new BalanceCalculator(DefaultSettings, days.Append(Start));
        Assert.Equal(TimeSpan.FromHours(88), calc.Month(2026, 10, new DateOnly(2026, 10, 31)).Target);
    }

    [Fact]
    public void SettimanaACavalloDelMese_ContaSoloIGiorniDelMese()
    {
        var calc = new BalanceCalculator(DefaultSettings, [Start]);
        var week = calc.Week(new DateOnly(2026, 10, 2));
        Assert.Equal(new DateOnly(2026, 10, 1), week.From);
        Assert.Equal(new DateOnly(2026, 10, 4), week.To);
        Assert.Equal(TimeSpan.FromHours(12), week.Target); // gio, ven, sab
    }

    [Fact]
    public void DebitoSettimanale_SiRecuperaLaSettimanaDopo()
    {
        WorkDay[] days =
        [
            Day(2026, 10, 1, 4), Day(2026, 10, 2, 4), Day(2026, 10, 3, 4), // in pari
            Day(2026, 10, 6, 4), Day(2026, 10, 7, 4), Day(2026, 10, 8, 4), Day(2026, 10, 9, 4), Day(2026, 10, 10, 2), // 18 h
        ];
        var calc = new BalanceCalculator(DefaultSettings, days.Append(Start));

        var month = calc.Month(2026, 10, new DateOnly(2026, 10, 10));
        Assert.Equal(TimeSpan.FromHours(-2), month.Balance);

        var nextWeek = calc.Week(new DateOnly(2026, 10, 13));
        Assert.Equal(TimeSpan.FromHours(-2), nextWeek.CarryIn);
        Assert.Equal(TimeSpan.FromHours(22), nextWeek.NeededThisWeek);
    }

    [Fact]
    public void GiorniPrimaDellaPrimaRegistrazione_NonContano()
    {
        // Inizia a usare l'app giovedì 15 ottobre: dall'1 al 14 non ha senso essere in debito.
        var calc = new BalanceCalculator(DefaultSettings, [Day(2026, 10, 15, 4)]);
        var month = calc.Month(2026, 10, new DateOnly(2026, 10, 15));
        Assert.Equal(TimeSpan.Zero, month.Balance);
        Assert.Equal(TimeSpan.FromHours(52), month.Target); // 13 giorni mar–sab dal 15 al 31
        Assert.Equal(TimeSpan.Zero, new BalanceCalculator(DefaultSettings, []).Month(2026, 10, new DateOnly(2026, 10, 31)).Target);
    }

    [Fact]
    public void SaldoSiAzzeraANuovoMese()
    {
        var calc = new BalanceCalculator(DefaultSettings, [Day(2026, 10, 31, 10)]);
        var week = calc.Week(new DateOnly(2026, 11, 3));
        Assert.Equal(TimeSpan.Zero, week.CarryIn);
    }

    [Fact]
    public void CambioMonteOre_NonToccaIMesiPassati()
    {
        var settings = new Settings();
        settings.Schedules.Add(WorkSchedule.Default with { ValidFrom = new DateOnly(2026, 11, 1), WeeklyHours = 25 });
        var calc = new BalanceCalculator(settings, [Start]);
        Assert.Equal(TimeSpan.FromHours(92), calc.Month(2026, 10, new DateOnly(2026, 10, 31)).Target);
        Assert.Equal(TimeSpan.FromHours(5), calc.Expected(new DateOnly(2026, 11, 3)));
    }
}

public class WorkDayTests
{
    private static TimeSlot Slot(int h1, int m1, int h2, int m2) => new(new TimeOnly(h1, m1), new TimeOnly(h2, m2));

    [Fact]
    public void PiuFasce_SiSommano()
    {
        var day = new WorkDay { Slots = [Slot(9, 0, 10, 0), Slot(11, 0, 12, 30), Slot(13, 30, 15, 0)] };
        Assert.Equal(TimeSpan.FromHours(4), day.Worked);
        Assert.Empty(day.Validate());
    }

    [Fact]
    public void FasceSovrapposteOInvertite_DannoErrore()
    {
        var day = new WorkDay { Slots = [Slot(9, 0, 11, 0), Slot(10, 30, 12, 0), Slot(15, 0, 14, 0)] };
        Assert.Equal(2, day.Validate().Count);
    }
}

public class ItalianHolidaysTests
{
    [Theory]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Pasqua(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), ItalianHolidays.EasterSunday(year));

    [Fact]
    public void SanFrancesco_SoloDal2026()
    {
        Assert.False(ItalianHolidays.For(2025).ContainsKey(new DateOnly(2025, 10, 4)));
        Assert.True(ItalianHolidays.For(2026).ContainsKey(new DateOnly(2026, 10, 4)));
    }
}
