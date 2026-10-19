namespace WorkHours.Core.Services;

/// <summary>Testi in italiano, senza dipendere dalla lingua impostata sul telefono.</summary>
public static class Format
{
    private static readonly string[] Months =
        ["gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno",
         "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre"];

    private static readonly string[] Days = ["domenica", "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato"];

    /// <summary>Intestazioni del calendario, da lunedì a domenica.</summary>
    public static IReadOnlyList<string> WeekHeaders { get; } = ["Lun", "Mar", "Mer", "Gio", "Ven", "Sab", "Dom"];

    public static string MonthName(int month) => Months[month - 1];

    public static string MonthYear(int year, int month) => $"{Capitalize(MonthName(month))} {year}";

    public static string DayName(DayOfWeek day) => Days[(int)day];

    /// <summary>Es. "martedì 20 ottobre".</summary>
    public static string LongDate(DateOnly date) => $"{DayName(date.DayOfWeek)} {date.Day} {MonthName(date.Month)}";

    /// <summary>Es. "4h", "4h 30", "0h".</summary>
    public static string Hours(TimeSpan value)
    {
        var minutes = (long)Math.Round(Math.Abs(value.TotalMinutes));
        var text = minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60:00}";
        return value < TimeSpan.Zero && minutes > 0 ? "−" + text : text;
    }

    /// <summary>Come <see cref="Hours"/> ma con il segno: "+2h", "−1h 30", "0h".</summary>
    public static string Balance(TimeSpan value)
    {
        var text = Hours(value);
        return value > TimeSpan.Zero && text != "0h" ? "+" + text : text;
    }

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
