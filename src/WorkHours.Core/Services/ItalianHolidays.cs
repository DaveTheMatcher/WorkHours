namespace WorkHours.Core.Services;

/// <summary>Festività nazionali italiane.</summary>
public static class ItalianHolidays
{
    public static IReadOnlyDictionary<DateOnly, string> For(int year)
    {
        var easter = EasterSunday(year);
        var list = new Dictionary<DateOnly, string>
        {
            [new(year, 1, 1)] = "Capodanno",
            [new(year, 1, 6)] = "Epifania",
            [easter] = "Pasqua",
            [easter.AddDays(1)] = "Lunedì dell'Angelo",
            [new(year, 4, 25)] = "Festa della Liberazione",
            [new(year, 5, 1)] = "Festa del Lavoro",
            [new(year, 6, 2)] = "Festa della Repubblica",
            [new(year, 8, 15)] = "Ferragosto",
            [new(year, 11, 1)] = "Ognissanti",
            [new(year, 12, 8)] = "Immacolata Concezione",
            [new(year, 12, 25)] = "Natale",
            [new(year, 12, 26)] = "Santo Stefano",
        };

        // San Francesco d'Assisi: di nuovo festa nazionale dal 2026.
        if (year >= 2026)
            list[new(year, 10, 4)] = "San Francesco d'Assisi";

        return list;
    }

    /// <summary>Domenica di Pasqua (calendario gregoriano, algoritmo di Meeus/Jones/Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
